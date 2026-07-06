using SellingNewProduct.Domain.Common;

namespace SellingNewProduct.Domain.Orders;

/// <summary>Raised when an order transitions from Draft to Confirmed.</summary>
public sealed class OrderConfirmedEvent : IDomainEvent
{
    public Guid OrderId { get; }

    public Guid CustomerId { get; }

    public decimal TotalAmount { get; }

    public string Currency { get; }

    public DateTime OccurredOnUtc { get; }

    public OrderConfirmedEvent(Guid theOrderId, Guid theCustomerId, decimal theTotalAmount, string theCurrency)
    {
        OrderId = theOrderId;
        CustomerId = theCustomerId;
        TotalAmount = theTotalAmount;
        Currency = theCurrency;
        OccurredOnUtc = DateTime.UtcNow;
    }
}
