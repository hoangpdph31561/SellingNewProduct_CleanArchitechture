using SellingNewProduct.Infrastructure.Messaging.Contracts;

namespace SellingNewProduct.Infrastructure.Messaging.Abstractions;

/// <summary>Which broker a message belongs on — decided AT THE SOURCE by its nature, not by funnelling
/// everything through one broker. A fact goes to Kafka; a work command goes to RabbitMQ.</summary>
public enum OutboxDestination
{
    /// <summary>A fact ("đã xảy ra"): broadcast on the Kafka log for fan-out, replay, analytics.</summary>
    Kafka = 0,

    /// <summary>A command ("hãy làm"): a RabbitMQ work item with ack + retry + dead-letter.</summary>
    RabbitMq = 1,
}

/// <summary>
/// What the router produces for one outgoing message, database-agnostic. Each database's outbox
/// writer turns these into its own row/document. <see cref="Route"/> is the Kafka topic or the
/// RabbitMQ queue; <see cref="MessageType"/> is the discriminator (event or command type name).
/// </summary>
public sealed record OutboxEntry(
    OutboxDestination Destination, string Route, string MessageType, string Payload, string PartitionKey);

/// <summary>
/// Publishes an already-serialized integration event to the Kafka backbone (a fact). The outbox
/// dispatcher owns the JSON + topic + type, so this stays a thin "put these bytes on that topic" seam.
/// </summary>
public interface IEventBus
{
    Task PublishAsync(string theTopic, string thePartitionKey, string theEventType, string thePayloadJson, CancellationToken theCancellationToken = default);
}

/// <summary>
/// Publishes an already-serialized command onto a RabbitMQ queue (a work item). Symmetric to
/// <see cref="IEventBus"/>: the dispatcher owns the JSON + queue + type. There is NO Kafka→RabbitMQ
/// bridge — the outbox router already decided this message is a command and sends it here directly.
/// </summary>
public interface ICommandPublisher
{
    Task PublishAsync(string theQueue, string theCommandType, string thePayloadJson, CancellationToken theCancellationToken = default);
}

/// <summary>
/// The transactional-outbox row as the dispatcher sees it. The concrete storage (a SQL table or a
/// Mongo collection) is owned by the database project; this port keeps messaging database-agnostic.
/// The dispatcher routes by <see cref="Destination"/> to the right broker.
/// </summary>
public sealed record OutboxMessage(
    Guid Id, OutboxDestination Destination, string Route, string MessageType, string Payload, string PartitionKey);

/// <summary>
/// Read/complete access to the outbox the background dispatcher polls. The business write persisted
/// these rows in the SAME transaction as the aggregate change, which is what makes "save + publish"
/// atomic without a distributed transaction.
/// </summary>
public interface IOutboxStore
{
    Task<IReadOnlyList<OutboxMessage>> GetUnpublishedAsync(int theBatchSize, CancellationToken theCancellationToken = default);

    Task MarkPublishedAsync(Guid theId, CancellationToken theCancellationToken = default);

    Task MarkFailedAsync(Guid theId, string theError, CancellationToken theCancellationToken = default);
}

/// <summary>Handles one integration-event type consumed from Kafka. Register one or more per event.</summary>
public interface IIntegrationEventHandler<in TEvent>
    where TEvent : IntegrationEvent
{
    Task HandleAsync(TEvent theEvent, CancellationToken theCancellationToken = default);
}

/// <summary>Handles one command type consumed from a RabbitMQ queue.</summary>
public interface ICommandHandler<in TCommand>
    where TCommand : MessagingCommand
{
    Task HandleAsync(TCommand theCommand, CancellationToken theCancellationToken = default);
}

// ----- Downstream side-effect ports (faked with logging here) -----------------------------------

public sealed record EmailMessage(string To, string Subject, string Body);

public interface IEmailSender
{
    Task SendAsync(EmailMessage theMessage, CancellationToken theCancellationToken = default);
}

public sealed record InvoiceRequest(Guid OrderId, Guid PaymentId, decimal Amount, string Currency);

public interface IInvoiceIssuer
{
    Task IssueAsync(InvoiceRequest theRequest, CancellationToken theCancellationToken = default);
}

public sealed record Notification(Guid CustomerId, string Channel, string Message);

public interface INotificationSender
{
    Task SendAsync(Notification theNotification, CancellationToken theCancellationToken = default);
}

public sealed record RestockAlert(Guid ProductId, string ProductName, int CurrentStock);

public interface IRestockAlerter
{
    Task AlertAsync(RestockAlert theAlert, CancellationToken theCancellationToken = default);
}
