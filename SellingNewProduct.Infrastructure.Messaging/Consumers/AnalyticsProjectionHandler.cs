using SellingNewProduct.Infrastructure.Messaging.Abstractions;
using SellingNewProduct.Infrastructure.Messaging.Contracts;
using SellingNewProduct.Infrastructure.Messaging.Services;

namespace SellingNewProduct.Infrastructure.Messaging.Consumers;

/// <summary>
/// An INDEPENDENT read-side consumer: it keeps a running sales/analytics projection off the same Kafka
/// events. It shares nothing with the notification consumer — a second consumer reading the same log
/// its own way is precisely the Kafka strength (fan-out to many, replayable). In microservices this
/// would be its own "analytics service" with its own database.
/// </summary>
public sealed class AnalyticsProjectionHandler :
    IIntegrationEventHandler<OrderPlacedIntegrationEvent>,
    IIntegrationEventHandler<OrderConfirmedIntegrationEvent>,
    IIntegrationEventHandler<OrderCancelledIntegrationEvent>,
    IIntegrationEventHandler<PaymentCompletedIntegrationEvent>,
    IIntegrationEventHandler<ProductStockChangedIntegrationEvent>
{
    private readonly IAnalyticsStore myAnalytics;

    public AnalyticsProjectionHandler(IAnalyticsStore theAnalytics)
    {
        myAnalytics = theAnalytics;
    }

    public Task HandleAsync(OrderPlacedIntegrationEvent theEvent, CancellationToken theCancellationToken = default)
    {
        myAnalytics.RecordOrderPlaced(theEvent.OrderId, theEvent.TotalAmount);
        return Task.CompletedTask;
    }

    public Task HandleAsync(OrderConfirmedIntegrationEvent theEvent, CancellationToken theCancellationToken = default)
    {
        myAnalytics.RecordOrderConfirmed(theEvent.OrderId, theEvent.TotalAmount);
        return Task.CompletedTask;
    }

    public Task HandleAsync(OrderCancelledIntegrationEvent theEvent, CancellationToken theCancellationToken = default)
    {
        myAnalytics.RecordOrderCancelled(theEvent.OrderId);
        return Task.CompletedTask;
    }

    public Task HandleAsync(PaymentCompletedIntegrationEvent theEvent, CancellationToken theCancellationToken = default)
    {
        myAnalytics.RecordRevenue(theEvent.Amount);
        return Task.CompletedTask;
    }

    public Task HandleAsync(ProductStockChangedIntegrationEvent theEvent, CancellationToken theCancellationToken = default)
    {
        myAnalytics.RecordStockMovement(theEvent.ProductId, theEvent.ChangeQuantity, theEvent.NewStock);
        return Task.CompletedTask;
    }
}
