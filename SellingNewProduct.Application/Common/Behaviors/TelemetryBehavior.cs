using System.Diagnostics;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using SellingNewProduct.Application.Common.Telemetry;
using SellingNewProduct.Domain.Common;

namespace SellingNewProduct.Application.Common.Behaviors;

/// <summary>
/// Cross-cutting observability for EVERY command/query — the single place that makes "just read the log
/// and you can see where the program got to, how long it took, and any exception" true for the whole
/// business layer. Runs as the OUTERMOST pipeline step (registered before validation) so it covers the
/// entire cost — validation included. For each request it produces all three signals at once:
/// <list type="bullet">
/// <item><b>Log</b> — a start line, an end line with the elapsed time and outcome, and (on failure) an
/// error line with the exception. Every line already carries the TraceId (Serilog enricher), so logs
/// tie back to the trace.</item>
/// <item><b>Trace</b> — a span per request (<see cref="AppTelemetry.ActivitySource"/>).</item>
/// <item><b>Metric</b> — RED counters/histogram (<see cref="AppTelemetry"/>).</item>
/// </list>
/// This is why individual handlers/domain services do NOT each need hand-written log lines: one behavior
/// covers them uniformly (and OpenTelemetry's EF Core instrumentation times every query underneath).
/// </summary>
public sealed class TelemetryBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger myLogger;

    public TelemetryBehavior(ILoggerFactory theLoggerFactory)
    {
        // One stable category for all requests ("...Application.Requests"), instead of a noisy generic
        // type name per closed behavior.
        myLogger = theLoggerFactory.CreateLogger("SellingNewProduct.Application.Requests");
    }

    public async Task<TResponse> Handle(
        TRequest theRequest, RequestHandlerDelegate<TResponse> theNext, CancellationToken theCancellationToken)
    {
        var aRequestName = typeof(TRequest).Name;

        using var aActivity = AppTelemetry.ActivitySource.StartActivity($"MediatR {aRequestName}", ActivityKind.Internal);
        aActivity?.SetTag("request.type", aRequestName);

        // "Where did we get to": one line as each request begins (Debug = quiet in prod, on in dev).
        myLogger.LogDebug("→ Handling {RequestName}", aRequestName);

        var aStopwatch = Stopwatch.StartNew();
        var aOutcome = "success";
        try
        {
            var aResponse = await theNext();

            aStopwatch.Stop();
            // "How long it took": end line with elapsed milliseconds.
            myLogger.LogInformation("✓ Handled {RequestName} in {ElapsedMs} ms", aRequestName, aStopwatch.Elapsed.TotalMilliseconds);
            return aResponse;
        }
        catch (Exception aException)
        {
            aStopwatch.Stop();
            aOutcome = "error";
            aActivity?.SetStatus(ActivityStatusCode.Error, aException.Message);
            aActivity?.SetTag("exception.type", aException.GetType().Name);

            // Expected business failures (validation / not-found / conflict / unauthorized / rule
            // violation) are logged at WARNING — they are normal 4xx outcomes, not bugs. Everything
            // else is an unexpected fault -> ERROR with the full stack trace.
            if (IsExpectedBusinessException(aException))
            {
                myLogger.LogWarning("✗ {RequestName} rejected after {ElapsedMs} ms: {ExceptionType} — {Message}",
                    aRequestName, aStopwatch.Elapsed.TotalMilliseconds, aException.GetType().Name, aException.Message);
            }
            else
            {
                myLogger.LogError(aException, "✗ {RequestName} FAILED after {ElapsedMs} ms ({ExceptionType})",
                    aRequestName, aStopwatch.Elapsed.TotalMilliseconds, aException.GetType().Name);
            }
            throw;
        }
        finally
        {
            // Same tag set on both instruments so metrics line up (Rate/Errors from the counter,
            // Duration from the histogram), sliced by request type and outcome.
            var aTags = new TagList
            {
                { "request.type", aRequestName },
                { "outcome", aOutcome },
            };
            AppTelemetry.RequestCount.Add(1, aTags);
            AppTelemetry.RequestDuration.Record(aStopwatch.Elapsed.TotalMilliseconds, aTags);
        }
    }

    // Known, expected outcomes mapped to 4xx by the API — not application bugs.
    private static bool IsExpectedBusinessException(Exception theException) => theException is
        ValidationException or DomainException or ConflictException or NotFoundException or UnauthorizedException;
}
