using System.Text;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SellingNewProduct.Infrastructure.Messaging.Abstractions;

namespace SellingNewProduct.Infrastructure.Messaging.Kafka;

/// <summary>
/// Kafka adapter for <see cref="IEventBus"/>. Holds one long-lived, idempotent producer (singleton).
/// The partition key is the aggregate id, so all events for the same order/payment land on the same
/// partition and therefore keep their order — the property Kafka gives you that a plain queue does not.
/// </summary>
public sealed class KafkaEventBus : IEventBus, IDisposable
{
    public const string EventTypeHeader = "eventType";

    private readonly IProducer<string, string> myProducer;
    private readonly ILogger<KafkaEventBus> myLogger;

    public KafkaEventBus(IOptions<KafkaSettings> theOptions, ILogger<KafkaEventBus> theLogger)
    {
        var aConfig = new ProducerConfig
        {
            BootstrapServers = theOptions.Value.BootstrapServers,
            // Idempotent, fully-acked writes: no duplicates from producer retries, no silent loss.
            Acks = Acks.All,
            EnableIdempotence = true,
            MessageSendMaxRetries = 5
        };

        myProducer = new ProducerBuilder<string, string>(aConfig).Build();
        myLogger = theLogger;
    }

    public async Task PublishAsync(
        string theTopic, string thePartitionKey, string theEventType, string thePayloadJson, CancellationToken theCancellationToken = default)
    {
        var aMessage = new Message<string, string>
        {
            Key = thePartitionKey,
            Value = thePayloadJson,
            Headers = new Headers { { EventTypeHeader, Encoding.UTF8.GetBytes(theEventType) } }
        };

        var aResult = await myProducer.ProduceAsync(theTopic, aMessage, theCancellationToken);
        myLogger.LogInformation(
            "Kafka ▶ published {EventType} to {Topic} [p{Partition}@{Offset}].",
            theEventType, theTopic, aResult.Partition.Value, aResult.Offset.Value);
    }

    public void Dispose()
    {
        myProducer.Flush(TimeSpan.FromSeconds(5));
        myProducer.Dispose();
    }
}
