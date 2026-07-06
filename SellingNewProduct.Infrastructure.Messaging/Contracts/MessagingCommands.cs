namespace SellingNewProduct.Infrastructure.Messaging.Contracts;

/// <summary>
/// A command is an instruction to "please do this piece of work" — the opposite of an integration
/// event (a fact). Commands travel over RabbitMQ, where exactly one worker picks each up, acks it
/// on success, and a failure is retried and finally dead-lettered. Where an event is broadcast to
/// many, a command is directed to one queue.
/// </summary>
public abstract record MessagingCommand;

/// <summary>The RabbitMQ queue names — the routing keys work commands are sent to.</summary>
public static class MessagingQueues
{
    public const string Exchange = "selling.commands";
    public const string DeadLetterExchange = "selling.commands.dlx";

    public const string SendEmail = "email.send";
    public const string IssueInvoice = "invoice.issue";
    public const string SendNotification = "notification.send";
    public const string RestockAlert = "restock.alert";
}

public sealed record SendEmailCommand(string To, string Subject, string Body) : MessagingCommand;

public sealed record IssueInvoiceCommand(Guid OrderId, Guid PaymentId, decimal Amount, string Currency) : MessagingCommand;

public sealed record SendNotificationCommand(Guid CustomerId, string Channel, string Message) : MessagingCommand;

public sealed record RestockAlertCommand(Guid ProductId, string ProductName, int CurrentStock) : MessagingCommand;
