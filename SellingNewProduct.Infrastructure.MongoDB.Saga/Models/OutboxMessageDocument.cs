namespace SellingNewProduct.Infrastructure.MongoDB.Saga.Models;

/// <summary>
/// One transactional-outbox document (collection <c>outbox_messages</c>) for the MongoDB side of the
/// saga provider. Written in the SAME Mongo transaction as the catalogue change that produced it (e.g.
/// a stock movement), so the fact and its intent-to-publish are atomic. The shared dispatcher drains
/// this alongside the SQL outbox and routes each by <see cref="Destination"/> to Kafka or RabbitMQ.
/// </summary>
internal sealed class OutboxMessageDocument
{
    public Guid Id { get; set; }

    /// <summary>0 = Kafka (fact), 1 = RabbitMQ (command). Matches <c>OutboxDestination</c>.</summary>
    public int Destination { get; set; }

    /// <summary>Kafka topic (fact) or RabbitMQ queue (command).</summary>
    public string Route { get; set; } = string.Empty;

    /// <summary>Discriminator: integration-event type name (Kafka) or command type name (RabbitMQ).</summary>
    public string MessageType { get; set; } = string.Empty;

    public string Payload { get; set; } = string.Empty;

    public string PartitionKey { get; set; } = string.Empty;

    public DateTime CreatedUtc { get; set; }

    public DateTime? ProcessedUtc { get; set; }

    public int RetryCount { get; set; }

    public string? Error { get; set; }
}
