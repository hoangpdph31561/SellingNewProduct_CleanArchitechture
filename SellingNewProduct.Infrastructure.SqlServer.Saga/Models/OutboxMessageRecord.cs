namespace SellingNewProduct.Infrastructure.SqlServer.Saga.Models;

/// <summary>
/// One transactional-outbox row. Written in the SAME SQL transaction as the Order/Payment change that
/// produced it, so the business fact and its intent-to-publish commit together (no dual-write gap). A
/// background dispatcher later reads unpublished rows and routes each — by <see cref="Destination"/> —
/// straight to Kafka (a fact) or RabbitMQ (a command), then stamps <see cref="ProcessedUtc"/>.
/// </summary>
internal sealed class OutboxMessageRecord
{
    public Guid Id { get; set; }

    /// <summary>0 = Kafka (fact), 1 = RabbitMQ (command). Matches <c>OutboxDestination</c>.</summary>
    public int Destination { get; set; }

    /// <summary>Kafka topic (fact) or RabbitMQ queue (command).</summary>
    public string Route { get; set; } = string.Empty;

    /// <summary>Discriminator: integration-event type name (Kafka) or command type name (RabbitMQ).</summary>
    public string MessageType { get; set; } = string.Empty;

    /// <summary>The serialized event/command.</summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>Kafka partition key (aggregate id) to keep a stream ordered. Empty for commands.</summary>
    public string PartitionKey { get; set; } = string.Empty;

    public DateTime CreatedUtc { get; set; }

    /// <summary>Null until the dispatcher publishes the row; the "unpublished" filter keys off this.</summary>
    public DateTime? ProcessedUtc { get; set; }

    public int RetryCount { get; set; }

    public string? Error { get; set; }
}
