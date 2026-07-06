using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using SellingNewProduct.Infrastructure.Messaging.Abstractions;

namespace SellingNewProduct.Infrastructure.Messaging.Services;

/// <summary>
/// Stand-in email gateway: logs instead of sending. Swap for a real SMTP/SendGrid adapter without
/// touching any consumer — the worker only knows the <see cref="IEmailSender"/> port.
/// </summary>
public sealed class LoggingEmailSender : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> myLogger;

    public LoggingEmailSender(ILogger<LoggingEmailSender> theLogger)
    {
        myLogger = theLogger;
    }

    public Task SendAsync(EmailMessage theMessage, CancellationToken theCancellationToken = default)
    {
        myLogger.LogInformation("📧 EMAIL → {To} | {Subject} | {Body}", theMessage.To, theMessage.Subject, theMessage.Body);
        return Task.CompletedTask;
    }
}

/// <summary>Stand-in invoice service: logs the issued invoice.</summary>
public sealed class LoggingInvoiceIssuer : IInvoiceIssuer
{
    private readonly ILogger<LoggingInvoiceIssuer> myLogger;

    public LoggingInvoiceIssuer(ILogger<LoggingInvoiceIssuer> theLogger)
    {
        myLogger = theLogger;
    }

    public Task IssueAsync(InvoiceRequest theRequest, CancellationToken theCancellationToken = default)
    {
        myLogger.LogInformation("🧾 INVOICE issued for order {OrderId} (payment {PaymentId}): {Amount:N0} {Currency}.",
            theRequest.OrderId, theRequest.PaymentId, theRequest.Amount, theRequest.Currency);
        return Task.CompletedTask;
    }
}

/// <summary>Stand-in notification gateway (push/sms/email): logs the notification.</summary>
public sealed class LoggingNotificationSender : INotificationSender
{
    private readonly ILogger<LoggingNotificationSender> myLogger;

    public LoggingNotificationSender(ILogger<LoggingNotificationSender> theLogger)
    {
        myLogger = theLogger;
    }

    public Task SendAsync(Notification theNotification, CancellationToken theCancellationToken = default)
    {
        myLogger.LogInformation("🔔 NOTIFY [{Channel}] customer {CustomerId}: {Message}",
            theNotification.Channel, theNotification.CustomerId, theNotification.Message);
        return Task.CompletedTask;
    }
}

/// <summary>Stand-in warehouse restock alerter: logs that a product ran out and needs reordering.</summary>
public sealed class LoggingRestockAlerter : IRestockAlerter
{
    private readonly ILogger<LoggingRestockAlerter> myLogger;

    public LoggingRestockAlerter(ILogger<LoggingRestockAlerter> theLogger)
    {
        myLogger = theLogger;
    }

    public Task AlertAsync(RestockAlert theAlert, CancellationToken theCancellationToken = default)
    {
        myLogger.LogWarning("⚠️ RESTOCK product {ProductId} '{Name}' is at {Stock} — reorder needed.",
            theAlert.ProductId, theAlert.ProductName, theAlert.CurrentStock);
        return Task.CompletedTask;
    }
}

/// <summary>A tiny running projection kept off the event stream — the analytics consumer's "database".</summary>
public interface IAnalyticsStore
{
    void RecordOrderPlaced(Guid theOrderId, decimal theAmount);
    void RecordOrderConfirmed(Guid theOrderId, decimal theAmount);
    void RecordOrderCancelled(Guid theOrderId);
    void RecordRevenue(decimal theAmount);
    void RecordStockMovement(Guid theProductId, int theChangeQuantity, int theNewStock);
}

/// <summary>In-memory analytics projection (singleton). Real deployments would persist this.</summary>
public sealed class InMemoryAnalyticsStore : IAnalyticsStore
{
    private readonly ILogger<InMemoryAnalyticsStore> myLogger;
    private readonly ConcurrentDictionary<string, decimal> myCounters = new();

    public InMemoryAnalyticsStore(ILogger<InMemoryAnalyticsStore> theLogger)
    {
        myLogger = theLogger;
    }

    public void RecordOrderPlaced(Guid theOrderId, decimal theAmount) => Bump("orders.placed", 1, theOrderId, theAmount);

    public void RecordOrderConfirmed(Guid theOrderId, decimal theAmount) => Bump("orders.confirmed", 1, theOrderId, theAmount);

    public void RecordOrderCancelled(Guid theOrderId) => Bump("orders.cancelled", 1, theOrderId, 0);

    public void RecordRevenue(decimal theAmount) => Bump("revenue.total", theAmount, Guid.Empty, theAmount);

    public void RecordStockMovement(Guid theProductId, int theChangeQuantity, int theNewStock) =>
        Bump("stock.movements", 1, theProductId, theNewStock);

    private void Bump(string theKey, decimal theDelta, Guid theOrderId, decimal theAmount)
    {
        var aValue = myCounters.AddOrUpdate(theKey, theDelta, (_, theExisting) => theExisting + theDelta);
        myLogger.LogInformation("📊 ANALYTICS {Key} = {Value} (order {OrderId}, amount {Amount:N0}).", theKey, aValue, theOrderId, theAmount);
    }
}
