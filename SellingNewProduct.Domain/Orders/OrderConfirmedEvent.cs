using SellingNewProduct.Domain.Common;

namespace SellingNewProduct.Domain.Orders;

/// <summary>Raised when an order transitions from Draft to Confirmed.</summary>
public sealed class OrderConfirmedEvent : IDomainEvent
{
    public Guid OrderId { get; }

    public Guid CustomerId { get; }

    public DateTime OccurredOnUtc { get; }

    public OrderConfirmedEvent(Guid theOrderId, Guid theCustomerId)
    {
        OrderId = theOrderId;
        CustomerId = theCustomerId;
        OccurredOnUtc = DateTime.UtcNow;
    }
}
