using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.ValueObjects;

namespace SellingNewProduct.Domain.Payments;

/// <summary>
/// A payment for an order. Aggregate root that references the order by id.
/// </summary>
public sealed class Payment : AggregateRoot<Guid>
{
    public Guid OrderId { get; private set; }

    public Money Amount { get; private set; } = default!;

    public PaymentMethod Method { get; private set; }

    public PaymentStatus PaymentStatus { get; private set; }

    public DateTime? PaidAtUtc { get; private set; }

    private Payment()
    {
    }

    private Payment(Guid theId, Guid theOrderId, Money theAmount, PaymentMethod theMethod)
        : base(theId)
    {
        OrderId = theOrderId;
        Amount = theAmount;
        Method = theMethod;
        PaymentStatus = PaymentStatus.Pending;
    }

    public static Payment Create(Guid theOrderId, Money theAmount, PaymentMethod theMethod)
    {
        if (theOrderId == Guid.Empty)
        {
            throw new DomainException("Payment must reference an order.");
        }

        if (theAmount.Amount <= 0)
        {
            throw new DomainException("Payment amount must be greater than zero.");
        }

        return new Payment(Guid.NewGuid(), theOrderId, theAmount, theMethod);
    }

    public void MarkCompleted()
    {
        if (PaymentStatus != PaymentStatus.Pending)
        {
            throw new DomainException($"Only a pending payment can be completed. Current status: {PaymentStatus}.");
        }

        PaymentStatus = PaymentStatus.Completed;
        PaidAtUtc = DateTime.UtcNow;
        MarkUpdated();
    }

    public void Refund()
    {
        if (PaymentStatus != PaymentStatus.Completed)
        {
            throw new DomainException("Only a completed payment can be refunded.");
        }

        PaymentStatus = PaymentStatus.Refunded;
        MarkUpdated();
    }

    public static Payment Rehydrate(
        Guid theId,
        Guid theOrderId,
        Money theAmount,
        PaymentMethod theMethod,
        PaymentStatus thePaymentStatus,
        DateTime? thePaidAtUtc,
        EntityStatus theStatus,
        DateTime theCreatedAtUtc,
        DateTime? theUpdatedAtUtc)
    {
        var aPayment = new Payment
        {
            OrderId = theOrderId,
            Amount = theAmount,
            Method = theMethod,
            PaymentStatus = thePaymentStatus,
            PaidAtUtc = thePaidAtUtc
        };

        aPayment.RestoreState(theId, theStatus, theCreatedAtUtc, theUpdatedAtUtc);
        return aPayment;
    }
}
