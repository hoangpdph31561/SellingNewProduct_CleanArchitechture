using System.Threading.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.RateLimiting;
using Polly.Registry;

namespace SellingNewProduct.Infrastructure.Saga.Core.Resilience;

/// <summary>
/// Base wrapper around a built cross-store <see cref="ResiliencePipeline"/>. It exists so the two
/// read directions get DISTINCT injectable types (below) instead of sharing one — that is what keeps
/// their resilience isolated. See <see cref="CrossStoreResilience"/> for why the split matters.
/// </summary>
public abstract class CrossStorePipeline
{
    protected CrossStorePipeline(ResiliencePipeline thePipeline) => Pipeline = thePipeline;

    public ResiliencePipeline Pipeline { get; }
}

/// <summary>Pipeline for the SQL-side read models reaching INTO MongoDB (customer/employee/category names).</summary>
public sealed class CrossStoreToMongoPipeline : CrossStorePipeline
{
    public CrossStoreToMongoPipeline(ResiliencePipeline thePipeline) : base(thePipeline) { }
}

/// <summary>Pipeline for the Mongo-side read models reaching INTO SQL (order stats/totals).</summary>
public sealed class CrossStoreToSqlPipeline : CrossStorePipeline
{
    public CrossStoreToSqlPipeline(ResiliencePipeline thePipeline) : base(thePipeline) { }
}

/// <summary>Tunables for the cross-store pipelines. Defaults are production-sane; override at registration.</summary>
public sealed class CrossStoreResilienceOptions
{
    /// <summary>Per-attempt timeout: abort a single hung cross-store query.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>Transient-failure retries before the call is considered failed.</summary>
    public int MaxRetryAttempts { get; set; } = 2;

    /// <summary>Base backoff delay (exponential + jitter is applied on top).</summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMilliseconds(150);

    /// <summary>Fraction of failed calls in the sampling window that trips the breaker.</summary>
    public double FailureRatio { get; set; } = 0.5;

    /// <summary>Rolling window the failure ratio is measured over.</summary>
    public TimeSpan SamplingDuration { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Minimum calls in the window before the breaker may trip (avoids tripping on 1 fluke).</summary>
    public int MinimumThroughput { get; set; } = 5;

    /// <summary>How long the breaker stays OPEN (fails fast) before probing with a trial call.</summary>
    public TimeSpan BreakDuration { get; set; } = TimeSpan.FromSeconds(20);

    /// <summary>
    /// Bulkhead: max cross-store reads (to a given store) running CONCURRENTLY. Kept well below the
    /// database driver's connection-pool size so these secondary enrichment reads can never starve the
    /// store's PRIMARY reads/writes of connections. Applied PER STORE (Mongo and SQL each get their own).
    /// </summary>
    public int MaxConcurrency { get; set; } = 10;

    /// <summary>Bulkhead queue: extra reads allowed to WAIT for a permit before excess is rejected fast.</summary>
    public int QueueLimit { get; set; } = 5;
}

/// <summary>
/// Builds the resilience for the cross-store read hops — the calls where a read model in one database
/// reaches into the OTHER database (SQL order reads fetching names from MongoDB; Mongo people reads
/// fetching order stats from SQL). Those are the closest thing to a remote-service call in this app,
/// so they are where these strategies earn their keep.
///
/// Two SEPARATE pipelines are registered, one per target store (<c>cross-store-to-mongo</c> and
/// <c>cross-store-to-sql</c>). Splitting by store is the whole point: a slow/failing Mongo must NOT
/// trip the breaker or exhaust the bulkhead of the perfectly healthy SQL-bound reads (and vice versa).
/// A single shared pipeline would put both "ship compartments" behind one wall.
///
/// Each pipeline layers (outermost first): bulkhead → retry → circuit breaker → timeout. Applied ONLY
/// to idempotent reads — writes run inside saga transactions with compensation and own their retries.
/// </summary>
public static class CrossStoreResilience
{
    public const string ToMongoPipelineKey = "cross-store-to-mongo";
    public const string ToSqlPipelineKey = "cross-store-to-sql";

    public static IServiceCollection AddCrossStoreResilience(
        this IServiceCollection theServices, Action<CrossStoreResilienceOptions>? theConfigure = null)
    {
        var aOptions = new CrossStoreResilienceOptions();
        theConfigure?.Invoke(aOptions);

        // One pipeline per target store — isolated failure counting AND isolated concurrency.
        theServices.AddResiliencePipeline(ToMongoPipelineKey, (theBuilder, theContext) =>
            ConfigurePipeline(theBuilder, theContext, aOptions, "to-Mongo"));
        theServices.AddResiliencePipeline(ToSqlPipelineKey, (theBuilder, theContext) =>
            ConfigurePipeline(theBuilder, theContext, aOptions, "to-SQL"));

        // Expose each built pipeline as its own typed wrapper so the decorators inject the right one.
        theServices.AddSingleton(theProvider => new CrossStoreToMongoPipeline(
            theProvider.GetRequiredService<ResiliencePipelineProvider<string>>().GetPipeline(ToMongoPipelineKey)));
        theServices.AddSingleton(theProvider => new CrossStoreToSqlPipeline(
            theProvider.GetRequiredService<ResiliencePipelineProvider<string>>().GetPipeline(ToSqlPipelineKey)));

        return theServices;
    }

    /// <summary>Shared layout for both per-store pipelines; <paramref name="theLabel"/> only tags the logs.</summary>
    private static void ConfigurePipeline(
        ResiliencePipelineBuilder theBuilder,
        Polly.DependencyInjection.AddResiliencePipelineContext<string> theContext,
        CrossStoreResilienceOptions theOptions,
        string theLabel)
    {
        var aLogger = theContext.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("CrossStoreResilience");

        // Bulkhead (outermost): a single shared ConcurrencyLimiter caps how many cross-store reads to
        // THIS store run at once, so a slow store cannot pile request threads onto the shared pool.
        // Built once here (the pipeline is a singleton) — do NOT new it inside the RateLimiter lambda.
        var aBulkhead = new ConcurrencyLimiter(new ConcurrencyLimiterOptions
        {
            PermitLimit = theOptions.MaxConcurrency,
            QueueLimit = theOptions.QueueLimit
        });

        theBuilder.AddRateLimiter(new RateLimiterStrategyOptions
        {
            RateLimiter = theArgs => aBulkhead.AcquireAsync(1, theArgs.Context.CancellationToken),
            OnRejected = theArgs =>
            {
                aLogger.LogWarning(
                    "Cross-store 🚧 bulkhead FULL ({Label}) — read rejected; {Permits} permits + {Queue} queue slots all in use.",
                    theLabel, theOptions.MaxConcurrency, theOptions.QueueLimit);
                return ValueTask.CompletedTask;
            }
        });

        // Retry: absorb brief blips. Sits outside the breaker so retries also count toward tripping it;
        // never retry once the breaker is already OPEN or on cancellation.
        theBuilder.AddRetry(new Polly.Retry.RetryStrategyOptions
        {
            MaxRetryAttempts = theOptions.MaxRetryAttempts,
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            Delay = theOptions.RetryDelay,
            ShouldHandle = new PredicateBuilder().Handle<Exception>(
                theEx => theEx is not (BrokenCircuitException or OperationCanceledException)),
            OnRetry = theArgs =>
            {
                aLogger.LogWarning("Cross-store ↻ retry {Attempt} ({Label}) after {Delay}ms ({Error}).",
                    theArgs.AttemptNumber + 1, theLabel, theArgs.RetryDelay.TotalMilliseconds, theArgs.Outcome.Exception?.Message);
                return ValueTask.CompletedTask;
            }
        });

        // Circuit breaker (middle): OPEN when too many cross-store calls to this store fail.
        theBuilder.AddCircuitBreaker(new CircuitBreakerStrategyOptions
        {
            FailureRatio = theOptions.FailureRatio,
            SamplingDuration = theOptions.SamplingDuration,
            MinimumThroughput = theOptions.MinimumThroughput,
            BreakDuration = theOptions.BreakDuration,
            ShouldHandle = new PredicateBuilder().Handle<Exception>(
                theEx => theEx is not OperationCanceledException),
            OnOpened = theArgs =>
            {
                aLogger.LogError("Cross-store ⛔ circuit OPEN ({Label}) for {Break}s — the other store is failing; reads fail fast.",
                    theLabel, theArgs.BreakDuration.TotalSeconds);
                return ValueTask.CompletedTask;
            },
            OnClosed = _ =>
            {
                aLogger.LogInformation("Cross-store ✅ circuit CLOSED ({Label}) — the other store recovered.", theLabel);
                return ValueTask.CompletedTask;
            },
            OnHalfOpened = _ =>
            {
                aLogger.LogInformation("Cross-store 🔎 circuit HALF-OPEN ({Label}) — probing the other store.", theLabel);
                return ValueTask.CompletedTask;
            }
        });

        // Timeout (innermost): a single hung query cannot wedge the request thread.
        theBuilder.AddTimeout(theOptions.Timeout);
    }
}
