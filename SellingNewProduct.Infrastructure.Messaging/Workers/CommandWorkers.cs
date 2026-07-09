using Polly;
using SellingNewProduct.Infrastructure.Messaging.Abstractions;
using SellingNewProduct.Infrastructure.Messaging.Contracts;

namespace SellingNewProduct.Infrastructure.Messaging.Workers;

/// <summary>Consumes <see cref="SendEmailCommand"/> from RabbitMQ and performs the actual send.
/// The gateway call runs through the shared Polly pipeline (timeout + retry + circuit breaker).</summary>
public sealed class EmailCommandWorker : ICommandHandler<SendEmailCommand>
{
    private readonly IEmailSender myEmailSender;
    private readonly ResiliencePipeline myResilience;

    public EmailCommandWorker(IEmailSender theEmailSender, ResiliencePipeline theResilience)
    {
        myEmailSender = theEmailSender;
        myResilience = theResilience;
    }

    public Task HandleAsync(SendEmailCommand theCommand, CancellationToken theCancellationToken = default) =>
        myResilience.ExecuteAsync(
            (theState, theCt) => new ValueTask(theState.myEmailSender.SendAsync(
                new EmailMessage(theCommand.To, theCommand.Subject, theCommand.Body), theCt)),
            this, theCancellationToken).AsTask();
}

/// <summary>Consumes <see cref="IssueInvoiceCommand"/> and issues the invoice/receipt.</summary>
public sealed class InvoiceCommandWorker : ICommandHandler<IssueInvoiceCommand>
{
    private readonly IInvoiceIssuer myInvoiceIssuer;
    private readonly ResiliencePipeline myResilience;

    public InvoiceCommandWorker(IInvoiceIssuer theInvoiceIssuer, ResiliencePipeline theResilience)
    {
        myInvoiceIssuer = theInvoiceIssuer;
        myResilience = theResilience;
    }

    public Task HandleAsync(IssueInvoiceCommand theCommand, CancellationToken theCancellationToken = default) =>
        myResilience.ExecuteAsync(
            (theState, theCt) => new ValueTask(theState.myInvoiceIssuer.IssueAsync(
                new InvoiceRequest(theCommand.OrderId, theCommand.PaymentId, theCommand.Amount, theCommand.Currency), theCt)),
            this, theCancellationToken).AsTask();
}

/// <summary>Consumes <see cref="SendNotificationCommand"/> and pushes the notification.</summary>
public sealed class NotificationCommandWorker : ICommandHandler<SendNotificationCommand>
{
    private readonly INotificationSender myNotificationSender;
    private readonly ResiliencePipeline myResilience;

    public NotificationCommandWorker(INotificationSender theNotificationSender, ResiliencePipeline theResilience)
    {
        myNotificationSender = theNotificationSender;
        myResilience = theResilience;
    }

    public Task HandleAsync(SendNotificationCommand theCommand, CancellationToken theCancellationToken = default) =>
        myResilience.ExecuteAsync(
            (theState, theCt) => new ValueTask(theState.myNotificationSender.SendAsync(
                new Notification(theCommand.CustomerId, theCommand.Channel, theCommand.Message), theCt)),
            this, theCancellationToken).AsTask();
}

/// <summary>Consumes <see cref="RestockAlertCommand"/> (raised when a product's stock hits zero).</summary>
public sealed class RestockAlertCommandWorker : ICommandHandler<RestockAlertCommand>
{
    private readonly IRestockAlerter myRestockAlerter;
    private readonly ResiliencePipeline myResilience;

    public RestockAlertCommandWorker(IRestockAlerter theRestockAlerter, ResiliencePipeline theResilience)
    {
        myRestockAlerter = theRestockAlerter;
        myResilience = theResilience;
    }

    public Task HandleAsync(RestockAlertCommand theCommand, CancellationToken theCancellationToken = default) =>
        myResilience.ExecuteAsync(
            (theState, theCt) => new ValueTask(theState.myRestockAlerter.AlertAsync(
                new RestockAlert(theCommand.ProductId, theCommand.ProductName, theCommand.CurrentStock), theCt)),
            this, theCancellationToken).AsTask();
}
