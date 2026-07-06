using SellingNewProduct.Domain.Common;

namespace SellingNewProduct.Domain.Orders;

/// <summary>Raised when an order is cancelled. <see cref="WasConfirmed"/> tells downstream whether stock had been reserved.</summary>
public sealed class OrderCancelledEvent : IDomainEvent
{
    public Guid OrderId { get; }

    public Guid CustomerId { get; }

    public bool WasConfirmed { get; }

    public DateTime OccurredOnUtc { get; }

    public OrderCancelledEvent(Guid theOrderId, Guid theCustomerId, bool theWasConfirmed)
    {
        OrderId = theOrderId;
        CustomerId = theCustomerId;
        WasConfirmed = theWasConfirmed;
        OccurredOnUtc = DateTime.UtcNow;
    }
}
