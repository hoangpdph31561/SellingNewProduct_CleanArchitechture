using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Registry;

namespace SellingNewProduct.Infrastructure.Saga.Core.Resilience;

/// <summary>
/// One <see cref="ResiliencePipeline"/> shared by the cross-store read hops — the calls where a read
/// model in one database reaches into the OTHER database (SQL order reads fetching customer/employee
/// names from MongoDB, Mongo people reads fetching order stats from SQL). Those are the closest thing
/// to a remote-service call in this app, so they are where a circuit breaker earns its keep: if the
/// other store is struggling, the breaker OPENS and the caller fails fast (and falls back to partial
/// data) instead of piling threads onto a sick dependency.
///
/// Applied ONLY to idempotent reads. Writes are deliberately left alone: they run inside saga
/// transactions with compensation, so an outer retry could double-apply — the saga owns their retries.
/// </summary>
public sealed class CrossStorePipeline
{
    public CrossStorePipeline(ResiliencePipeline thePipeline) => Pipeline = thePipeline;

    public ResiliencePipeline Pipeline { get; }
}

/// <summary>Tunables for the cross-store pipeline. Defaults are production-sane; override at registration.</summary>
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
}

public static class CrossStoreResilience
{
    public const string PipelineKey = "cross-store-reads";

    public static IServiceCollection AddCrossStoreResilience(
        this IServiceCollection theServices, Action<CrossStoreResilienceOptions>? theConfigure = null)
    {
        var aOptions = new CrossStoreResilienceOptions();
        theConfigure?.Invoke(aOptions);

        theServices.AddResiliencePipeline(PipelineKey, (theBuilder, theContext) =>
        {
            var aLogger = theContext.ServiceProvider
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("CrossStoreResilience");

            // Retry (outermost): absorb brief blips. Sits outside the breaker so retries also count
            // toward tripping it; never retry once the breaker is already OPEN or on cancellation.
            theBuilder.AddRetry(new Polly.Retry.RetryStrategyOptions
            {
                MaxRetryAttempts = aOptions.MaxRetryAttempts,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = aOptions.RetryDelay,
                ShouldHandle = new PredicateBuilder().Handle<Exception>(
                    theEx => theEx is not (BrokenCircuitException or OperationCanceledException)),
                OnRetry = theArgs =>
                {
                    aLogger.LogWarning("Cross-store ↻ retry {Attempt} after {Delay}ms ({Error}).",
                        theArgs.AttemptNumber + 1, theArgs.RetryDelay.TotalMilliseconds, theArgs.Outcome.Exception?.Message);
                    return ValueTask.CompletedTask;
                }
            });

            // Circuit breaker (middle): OPEN when too many cross-store calls fail, then short-circuit.
            theBuilder.AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = aOptions.FailureRatio,
                SamplingDuration = aOptions.SamplingDuration,
                MinimumThroughput = aOptions.MinimumThroughput,
                BreakDuration = aOptions.BreakDuration,
                ShouldHandle = new PredicateBuilder().Handle<Exception>(
                    theEx => theEx is not OperationCanceledException),
                OnOpened = theArgs =>
                {
                    aLogger.LogError("Cross-store ⛔ circuit OPEN for {Break}s — the other store is failing; reads fail fast.",
                        theArgs.BreakDuration.TotalSeconds);
                    return ValueTask.CompletedTask;
                },
                OnClosed = _ =>
                {
                    aLogger.LogInformation("Cross-store ✅ circuit CLOSED — the other store recovered.");
                    return ValueTask.CompletedTask;
                },
                OnHalfOpened = _ =>
                {
                    aLogger.LogInformation("Cross-store 🔎 circuit HALF-OPEN — probing the other store.");
                    return ValueTask.CompletedTask;
                }
            });

            // Timeout (innermost): a single hung query cannot wedge the request thread.
            theBuilder.AddTimeout(aOptions.Timeout);
        });

        // Expose the built pipeline as a plain singleton so the decorators inject it directly.
        theServices.AddSingleton(theProvider => new CrossStorePipeline(
            theProvider.GetRequiredService<ResiliencePipelineProvider<string>>().GetPipeline(PipelineKey)));

        return theServices;
    }
}
