namespace SellingNewProduct.Infrastructure.Messaging.Contracts;

/// <summary>
/// The public, cross-service contract for "something that has happened" — distinct from a domain
/// event (which is internal to the Domain). Integration events are what travels over Kafka: durable,
/// replayable facts that any number of consumer groups (a future microservice each) can read
/// independently. Keep them flat, serializable, and free of Domain types so a consumer that does not
/// reference the Domain can still deserialize them.
/// </summary>
public abstract record IntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();

    public DateTime OccurredOnUtc { get; init; } = DateTime.UtcNow;
}

/// <summary>The Kafka topics this system publishes to. One topic per aggregate stream.</summary>
public static class MessagingTopics
{
    public const string OrderEvents = "orders.events";
    public const string PaymentEvents = "payments.events";
    public const string ProductEvents = "products.events";
}

public sealed record OrderPlacedIntegrationEvent(
    Guid OrderId, Guid CustomerId, Guid EmployeeId, decimal TotalAmount, string Currency) : IntegrationEvent;

public sealed record OrderConfirmedIntegrationEvent(
    Guid OrderId, Guid CustomerId, decimal TotalAmount, string Currency) : IntegrationEvent;

public sealed record OrderShippedIntegrationEvent(Guid OrderId, Guid CustomerId) : IntegrationEvent;

public sealed record OrderCancelledIntegrationEvent(Guid OrderId, Guid CustomerId, bool WasConfirmed) : IntegrationEvent;

public sealed record PaymentCompletedIntegrationEvent(
    Guid PaymentId, Guid OrderId, decimal Amount, string Currency) : IntegrationEvent;

public sealed record ProductStockChangedIntegrationEvent(
    Guid ProductId, string ProductName, int NewStock, int ChangeQuantity) : IntegrationEvent;
