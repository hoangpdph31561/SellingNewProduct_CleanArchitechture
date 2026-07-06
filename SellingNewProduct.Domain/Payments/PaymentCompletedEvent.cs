using SellingNewProduct.Domain.Common;

namespace SellingNewProduct.Domain.Payments;

/// <summary>Raised when a payment is completed — the trigger for issuing an invoice/receipt.</summary>
public sealed class PaymentCompletedEvent : IDomainEvent
{
    public Guid PaymentId { get; }

    public Guid OrderId { get; }

    public decimal Amount { get; }

    public string Currency { get; }

    public DateTime OccurredOnUtc { get; }

    public PaymentCompletedEvent(Guid thePaymentId, Guid theOrderId, decimal theAmount, string theCurrency)
    {
        PaymentId = thePaymentId;
        OrderId = theOrderId;
        Amount = theAmount;
        Currency = theCurrency;
        OccurredOnUtc = DateTime.UtcNow;
    }
}
