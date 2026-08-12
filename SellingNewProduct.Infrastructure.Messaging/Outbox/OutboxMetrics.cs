using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace SellingNewProduct.Infrastructure.Messaging.Outbox;

/// <summary>
/// Metrics for the outbox relay, exported via OpenTelemetry (the API subscribes to
/// <see cref="MeterName"/>). Answers "is the event pipeline healthy?" at a glance: how many messages
/// were published vs failed, and how long publishing takes — sliced by broker (Kafka/RabbitMQ) and
/// message type. Pure BCL <see cref="Meter"/>, so the messaging layer needs no telemetry vendor SDK.
/// Registered as a singleton (one Meter instance for the whole process).
/// </summary>
public sealed class OutboxMetrics : IDisposable
{
    public const string MeterName = "SellingNewProduct.Messaging.Outbox";

    private readonly Meter myMeter;
    private readonly Counter<long> myPublished;
    private readonly Counter<long> myFailed;
    private readonly Histogram<double> myPublishDuration;

    public OutboxMetrics()
    {
        myMeter = new Meter(MeterName, "1.0.0");
        myPublished = myMeter.CreateCounter<long>("outbox.published.total", unit: "{message}",
            description: "Outbox messages published successfully, tagged by destination and type.");
        myFailed = myMeter.CreateCounter<long>("outbox.failed.total", unit: "{message}",
            description: "Outbox publish attempts that failed (will be retried), tagged likewise.");
        myPublishDuration = myMeter.CreateHistogram<double>("outbox.publish.duration", unit: "ms",
            description: "Time to publish one outbox message to its broker.");
    }

    public void Published(string theDestination, string theMessageType, double theElapsedMs)
    {
        var aTags = new TagList { { "destination", theDestination }, { "type", theMessageType } };
        myPublished.Add(1, aTags);
        myPublishDuration.Record(theElapsedMs, aTags);
    }

    public void Failed(string theDestination, string theMessageType) =>
        myFailed.Add(1, new TagList { { "destination", theDestination }, { "type", theMessageType } });

    public void Dispose() => myMeter.Dispose();
}
