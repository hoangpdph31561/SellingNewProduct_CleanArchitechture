using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace SellingNewProduct.Application.Common.Telemetry;

/// <summary>
/// The application's own telemetry sources — built on the BCL primitives (<see cref="ActivitySource"/>
/// for traces, <see cref="Meter"/> for metrics), so the Application layer stays free of any vendor
/// SDK. OpenTelemetry in the API subscribes to these by NAME (<see cref="ActivitySourceName"/> /
/// <see cref="MeterName"/>) and exports them; if nothing subscribes, these calls are almost free.
///
/// Every command/query that flows through MediatR is wrapped by <c>TelemetryBehavior</c>, which opens
/// a span here and records the RED signals (Rate = request count, Errors = failure count, Duration =
/// latency histogram) per request type.
/// </summary>
public static class AppTelemetry
{
    public const string ActivitySourceName = "SellingNewProduct.Application";
    public const string MeterName = "SellingNewProduct.Application";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName, "1.0.0");

    private static readonly Meter Meter = new(MeterName, "1.0.0");

    /// <summary>Rate + Errors: total requests handled, tagged by request type and outcome.</summary>
    public static readonly Counter<long> RequestCount =
        Meter.CreateCounter<long>("app.request.total", unit: "{request}",
            description: "Total MediatR requests handled, tagged by request type and outcome.");

    /// <summary>Duration: handler latency distribution, tagged by request type.</summary>
    public static readonly Histogram<double> RequestDuration =
        Meter.CreateHistogram<double>("app.request.duration", unit: "ms",
            description: "MediatR request handling duration in milliseconds.");
}
