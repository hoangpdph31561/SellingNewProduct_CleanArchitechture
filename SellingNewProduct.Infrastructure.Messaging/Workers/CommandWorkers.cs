using SellingNewProduct.Infrastructure.Messaging.Abstractions;
using SellingNewProduct.Infrastructure.Messaging.Contracts;

namespace SellingNewProduct.Infrastructure.Messaging.Workers;

/// <summary>Consumes <see cref="SendEmailCommand"/> from RabbitMQ and performs the actual send.</summary>
public sealed class EmailCommandWorker : ICommandHandler<SendEmailCommand>
{
    private readonly IEmailSender myEmailSender;

    public EmailCommandWorker(IEmailSender theEmailSender)
    {
        myEmailSender = theEmailSender;
    }

    public Task HandleAsync(SendEmailCommand theCommand, CancellationToken theCancellationToken = default) =>
        myEmailSender.SendAsync(new EmailMessage(theCommand.To, theCommand.Subject, theCommand.Body), theCancellationToken);
}

/// <summary>Consumes <see cref="IssueInvoiceCommand"/> and issues the invoice/receipt.</summary>
public sealed class InvoiceCommandWorker : ICommandHandler<IssueInvoiceCommand>
{
    private readonly IInvoiceIssuer myInvoiceIssuer;

    public InvoiceCommandWorker(IInvoiceIssuer theInvoiceIssuer)
    {
        myInvoiceIssuer = theInvoiceIssuer;
    }

    public Task HandleAsync(IssueInvoiceCommand theCommand, CancellationToken theCancellationToken = default) =>
        myInvoiceIssuer.IssueAsync(
            new InvoiceRequest(theCommand.OrderId, theCommand.PaymentId, theCommand.Amount, theCommand.Currency), theCancellationToken);
}

/// <summary>Consumes <see cref="SendNotificationCommand"/> and pushes the notification.</summary>
public sealed class NotificationCommandWorker : ICommandHandler<SendNotificationCommand>
{
    private readonly INotificationSender myNotificationSender;

    public NotificationCommandWorker(INotificationSender theNotificationSender)
    {
        myNotificationSender = theNotificationSender;
    }

    public Task HandleAsync(SendNotificationCommand theCommand, CancellationToken theCancellationToken = default) =>
        myNotificationSender.SendAsync(
            new Notification(theCommand.CustomerId, theCommand.Channel, theCommand.Message), theCancellationToken);
}

/// <summary>Consumes <see cref="RestockAlertCommand"/> (raised when a product's stock hits zero).</summary>
public sealed class RestockAlertCommandWorker : ICommandHandler<RestockAlertCommand>
{
    private readonly IRestockAlerter myRestockAlerter;

    public RestockAlertCommandWorker(IRestockAlerter theRestockAlerter)
    {
        myRestockAlerter = theRestockAlerter;
    }

    public Task HandleAsync(RestockAlertCommand theCommand, CancellationToken theCancellationToken = default) =>
        myRestockAlerter.AlertAsync(
            new RestockAlert(theCommand.ProductId, theCommand.ProductName, theCommand.CurrentStock), theCancellationToken);
}
