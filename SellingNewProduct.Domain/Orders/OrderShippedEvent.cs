using SellingNewProduct.Domain.Common;

namespace SellingNewProduct.Domain.Orders;

/// <summary>Raised when a confirmed order is marked as shipped.</summary>
public sealed class OrderShippedEvent : IDomainEvent
{
    public Guid OrderId { get; }

    public Guid CustomerId { get; }

    public DateTime OccurredOnUtc { get; }

    public OrderShippedEvent(Guid theOrderId, Guid theCustomerId)
    {
        OrderId = theOrderId;
        CustomerId = theCustomerId;
        OccurredOnUtc = DateTime.UtcNow;
    }
}
