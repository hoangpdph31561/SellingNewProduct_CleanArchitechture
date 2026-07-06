using SellingNewProduct.Domain.Common;

namespace SellingNewProduct.Domain.Orders;

/// <summary>Raised when a brand-new order has been placed (created as Draft with all its lines).</summary>
public sealed class OrderPlacedEvent : IDomainEvent
{
    public Guid OrderId { get; }

    public Guid CustomerId { get; }

    public Guid EmployeeId { get; }

    public decimal TotalAmount { get; }

    public string Currency { get; }

    public DateTime OccurredOnUtc { get; }

    public OrderPlacedEvent(Guid theOrderId, Guid theCustomerId, Guid theEmployeeId, decimal theTotalAmount, string theCurrency)
    {
        OrderId = theOrderId;
        CustomerId = theCustomerId;
        EmployeeId = theEmployeeId;
        TotalAmount = theTotalAmount;
        Currency = theCurrency;
        OccurredOnUtc = DateTime.UtcNow;
    }
}
