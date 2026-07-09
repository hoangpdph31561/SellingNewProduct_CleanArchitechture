using System.Threading.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.RateLimiting;
using Polly.Registry;
using Polly.Retry;
using Polly.Timeout;

namespace SellingNewProduct.Infrastructure.Messaging.Resilience;

/// <summary>
/// A shared Polly resilience pipeline that wraps every call to a downstream side-effect gateway
/// (email, invoice, notification, restock). These gateways stand in for real external services
/// (SMTP/SendGrid, an invoice API, a push provider) — the failure points a circuit breaker exists
/// to protect. When a gateway starts failing, the breaker OPENS and fails fast for a cool-down
/// window instead of letting a command worker hammer a dead dependency and pile up work.
///
/// Strategy order (outermost first — the "onion"): bulkhead → retry → circuit breaker → timeout.
///   • Timeout  (innermost): abort a single attempt that hangs, so one stuck call cannot block a worker.
///   • Breaker  (middle):    trip after too many failures in the sampling window; short-circuit while OPEN.
///   • Retry    (middle):    absorb brief blips. It sits OUTSIDE the breaker so retries also count
///                            toward tripping it, and a BrokenCircuitException stops retrying instantly.
///   • Bulkhead (outermost): the concurrency limiter — cap how many calls may be IN FLIGHT at once
///                            (plus a small waiting queue). This is resource ISOLATION: a gateway that
///                            slows down can only ever tie up N permits, so it cannot drain the entire
///                            thread/connection pool and starve the other, healthy workers. Excess work
///                            is rejected fast with a RateLimiterRejectedException. It is the outermost
///                            layer so a permit is held for the WHOLE retry sequence, not per attempt.
/// </summary>
public static class DownstreamResilience
{
    /// <summary>Registry key for the pipeline shared by all downstream-gateway command workers.</summary>
    public const string PipelineKey = "downstream-side-effects";

    public static IServiceCollection AddDownstreamResilience(this IServiceCollection theServices)
    {
        theServices.AddResiliencePipeline(PipelineKey, static (theBuilder, theContext) =>
        {
            var aLogger = theContext.ServiceProvider
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("DownstreamResilience");

            // 4) Bulkhead (outermost): a single shared concurrency limiter isolates in-flight calls.
            //    Only PermitLimit calls run at once; up to QueueLimit more wait for a permit; anything
            //    beyond that is rejected immediately (RateLimiterRejectedException) rather than piling
            //    up. Built ONCE here (the pipeline is a singleton) so every worker shares the same pool.
            var aBulkhead = new ConcurrencyLimiter(new ConcurrencyLimiterOptions
            {
                PermitLimit = 8,
                QueueLimit = 4
            });

            theBuilder.AddRateLimiter(new RateLimiterStrategyOptions
            {
                RateLimiter = theArgs => aBulkhead.AcquireAsync(1, theArgs.Context.CancellationToken),
                OnRejected = theArgs =>
                {
                    aLogger.LogWarning(
                        "Polly 🚧 bulkhead FULL — call rejected; {Permits} permits + {Queue} queue slots all in use.",
                        8, 4);
                    return ValueTask.CompletedTask;
                }
            });

            // 3) Retry brief, transient failures with exponential backoff + jitter.
            theBuilder.AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = TimeSpan.FromMilliseconds(200),
                // Do NOT retry when the breaker is already OPEN — fail fast instead.
                ShouldHandle = new PredicateBuilder()
                    .Handle<Exception>(theEx => theEx is not BrokenCircuitException),
                OnRetry = theArgs =>
                {
                    aLogger.LogWarning(
                        "Polly ↻ retry {Attempt} after {Delay}ms due to {Error}.",
                        theArgs.AttemptNumber + 1, theArgs.RetryDelay.TotalMilliseconds, theArgs.Outcome.Exception?.Message);
                    return ValueTask.CompletedTask;
                }
            });

            // 2) Circuit breaker: OPEN once >=50% of calls fail across a 10s window (min 4 calls),
            //    then short-circuit for 15s before letting a trial call through (HALF-OPEN).
            theBuilder.AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                SamplingDuration = TimeSpan.FromSeconds(10),
                MinimumThroughput = 4,
                BreakDuration = TimeSpan.FromSeconds(15),
                OnOpened = theArgs =>
                {
                    aLogger.LogError(
                        "Polly ⛔ circuit OPEN for {Break}s — downstream gateway is failing; calls fail fast.",
                        theArgs.BreakDuration.TotalSeconds);
                    return ValueTask.CompletedTask;
                },
                OnClosed = _ =>
                {
                    aLogger.LogInformation("Polly ✅ circuit CLOSED — downstream gateway recovered.");
                    return ValueTask.CompletedTask;
                },
                OnHalfOpened = _ =>
                {
                    aLogger.LogInformation("Polly 🔎 circuit HALF-OPEN — probing the downstream gateway.");
                    return ValueTask.CompletedTask;
                }
            });

            // 1) Per-attempt timeout: a single hung call is aborted so it cannot wedge a worker (innermost).
            theBuilder.AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(3),
                OnTimeout = theArgs =>
                {
                    aLogger.LogWarning("Polly ⏱ attempt timed out after {Timeout}s.", theArgs.Timeout.TotalSeconds);
                    return ValueTask.CompletedTask;
                }
            });
        });

        // Expose the built pipeline as a plain singleton so the command workers can inject it directly
        // instead of resolving it from the provider by key on every message.
        theServices.AddSingleton(theProvider =>
            theProvider.GetRequiredService<ResiliencePipelineProvider<string>>().GetPipeline(PipelineKey));

        return theServices;
    }
}
