using System.Text.Json;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Orders;
using SellingNewProduct.Domain.Payments;
using SellingNewProduct.Domain.Products;
using SellingNewProduct.Infrastructure.Messaging.Abstractions;
using SellingNewProduct.Infrastructure.Messaging.Contracts;
using SellingNewProduct.Infrastructure.Messaging.Routing;

namespace SellingNewProduct.Infrastructure.Saga.Core.Outbox;

/// <summary>
/// The single policy that decides, at the SOURCE, where each domain event's messages go — a FACT to
/// Kafka, a COMMAND to RabbitMQ — and produces the database-agnostic <see cref="OutboxEntry"/> rows a
/// database's outbox writer will persist. This is the heart of "route by nature": one confirm both
/// announces a fact (analytics/other services read it off Kafka) AND enqueues concrete work
/// (send-email, notify) straight onto RabbitMQ. No message is funnelled through Kafka just to reach
/// RabbitMQ. Shared by the SQL and MongoDB outbox writers, so both databases route the same way.
/// </summary>
public sealed class OutboxRouter
{
    private readonly IntegrationEventRegistry myEvents;
    private readonly CommandRegistry myCommands;

    public OutboxRouter(IntegrationEventRegistry theEvents, CommandRegistry theCommands)
    {
        myEvents = theEvents;
        myCommands = theCommands;
    }

    /// <summary>Turns an aggregate's domain events into the outbox entries to persist.</summary>
    public IReadOnlyList<OutboxEntry> Route(IEnumerable<IDomainEvent> theDomainEvents)
    {
        var aEntries = new List<OutboxEntry>();

        foreach (var aDomainEvent in theDomainEvents)
        {
            aEntries.AddRange(Map(aDomainEvent));
        }

        return aEntries;
    }

    private IEnumerable<OutboxEntry> Map(IDomainEvent theDomainEvent) => theDomainEvent switch
    {
        // Placement: a fact for analytics/other services. No command needed yet.
        OrderPlacedEvent e => new[]
        {
            Fact(new OrderPlacedIntegrationEvent(e.OrderId, e.CustomerId, e.EmployeeId, e.TotalAmount, e.Currency) { OccurredOnUtc = e.OccurredOnUtc }, e.OrderId.ToString())
        },

        // Confirmation: a fact AND two concrete work commands (email + push) — routed directly.
        OrderConfirmedEvent e => new[]
        {
            Fact(new OrderConfirmedIntegrationEvent(e.OrderId, e.CustomerId, e.TotalAmount, e.Currency) { OccurredOnUtc = e.OccurredOnUtc }, e.OrderId.ToString()),
            Command(new SendEmailCommand(ToEmail(e.CustomerId), $"Đơn hàng {e.OrderId} đã được xác nhận", $"Đơn {e.OrderId} trị giá {e.TotalAmount:N0} {e.Currency} đã được xác nhận.")),
            Command(new SendNotificationCommand(e.CustomerId, "push", $"Đơn {e.OrderId} đã xác nhận."))
        },

        OrderShippedEvent e => new[]
        {
            Fact(new OrderShippedIntegrationEvent(e.OrderId, e.CustomerId) { OccurredOnUtc = e.OccurredOnUtc }, e.OrderId.ToString()),
            Command(new SendNotificationCommand(e.CustomerId, "sms", $"Đơn {e.OrderId} đang được giao."))
        },

        OrderCancelledEvent e => new[]
        {
            Fact(new OrderCancelledIntegrationEvent(e.OrderId, e.CustomerId, e.WasConfirmed) { OccurredOnUtc = e.OccurredOnUtc }, e.OrderId.ToString()),
            Command(new SendNotificationCommand(e.CustomerId, "email", $"Đơn {e.OrderId} đã bị huỷ."))
        },

        // Payment done: a fact AND the issue-invoice work command.
        PaymentCompletedEvent e => new[]
        {
            Fact(new PaymentCompletedIntegrationEvent(e.PaymentId, e.OrderId, e.Amount, e.Currency) { OccurredOnUtc = e.OccurredOnUtc }, e.OrderId.ToString()),
            Command(new IssueInvoiceCommand(e.OrderId, e.PaymentId, e.Amount, e.Currency))
        },

        // Stock movement (MongoDB side): always a fact for analytics; if it hit zero, also a restock
        // command. This is what makes the catalogue (Mongo) a first-class participant, not just Order.
        ProductStockChangedEvent e => e.NewStock == 0
            ? new[]
            {
                Fact(new ProductStockChangedIntegrationEvent(e.ProductId, e.ProductName, e.NewStock, e.ChangeQuantity) { OccurredOnUtc = e.OccurredOnUtc }, e.ProductId.ToString()),
                Command(new RestockAlertCommand(e.ProductId, e.ProductName, e.NewStock))
            }
            : new[]
            {
                Fact(new ProductStockChangedIntegrationEvent(e.ProductId, e.ProductName, e.NewStock, e.ChangeQuantity) { OccurredOnUtc = e.OccurredOnUtc }, e.ProductId.ToString())
            },

        _ => Array.Empty<OutboxEntry>()
    };

    /// <summary>A fact → Kafka (topic + discriminator from the event registry).</summary>
    private OutboxEntry Fact(IntegrationEvent theEvent, string thePartitionKey)
    {
        var aRegistration = myEvents.Describe(theEvent.GetType());
        var aPayload = JsonSerializer.Serialize(theEvent, theEvent.GetType(), MessagingJson.Options);
        return new OutboxEntry(OutboxDestination.Kafka, aRegistration.Topic, aRegistration.EventType, aPayload, thePartitionKey);
    }

    /// <summary>A command → RabbitMQ (queue from the command registry).</summary>
    private OutboxEntry Command(MessagingCommand theCommand)
    {
        var aQueue = myCommands.QueueFor(theCommand.GetType());
        var aPayload = JsonSerializer.Serialize(theCommand, theCommand.GetType(), MessagingJson.Options);
        return new OutboxEntry(OutboxDestination.RabbitMq, aQueue, theCommand.GetType().Name, aPayload, string.Empty);
    }

    // A real system resolves the customer's contact from a directory; here we synthesize it.
    private static string ToEmail(Guid theCustomerId) => $"customer-{theCustomerId:N}@example.com";
}
