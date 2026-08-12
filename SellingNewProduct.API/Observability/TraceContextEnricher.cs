using System.Diagnostics;
using Serilog.Core;
using Serilog.Events;

namespace SellingNewProduct.API.Observability;

/// <summary>
/// Stamps every log line with the current <see cref="Activity"/>'s TraceId/SpanId, so a log entry can
/// be jumped straight to its distributed trace (and vice-versa) — the "correlation" that ties Logs to
/// Traces. Pure BCL: it reads <see cref="Activity.Current"/>, which OpenTelemetry's ASP.NET Core
/// instrumentation has already set for the request.
/// </summary>
public sealed class TraceContextEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent theLogEvent, ILogEventPropertyFactory thePropertyFactory)
    {
        var aActivity = Activity.Current;
        if (aActivity is null)
        {
            return;
        }

        theLogEvent.AddPropertyIfAbsent(thePropertyFactory.CreateProperty("TraceId", aActivity.TraceId.ToString()));
        theLogEvent.AddPropertyIfAbsent(thePropertyFactory.CreateProperty("SpanId", aActivity.SpanId.ToString()));
    }
}
