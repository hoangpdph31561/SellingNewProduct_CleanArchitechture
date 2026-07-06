namespace SellingNewProduct.Infrastructure.Messaging.Kafka;

/// <summary>Connection + consumer-group settings for the Kafka event backbone.</summary>
public sealed class KafkaSettings
{
    public const string SectionName = "Kafka";

    public string BootstrapServers { get; set; } = "localhost:9092";

    /// <summary>The consumer group. A different group replays the whole log independently — that is
    /// exactly how a future microservice would join without disturbing existing consumers.</summary>
    public string ConsumerGroupId { get; set; } = "selling-newproduct";
}
