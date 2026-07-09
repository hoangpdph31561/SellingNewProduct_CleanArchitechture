using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Interfaces.Inbound;
using SellingNewProduct.Domain.Interfaces.Outbound;
using SellingNewProduct.Domain.Orders;
using SellingNewProduct.Domain.Payments;
using SellingNewProduct.Domain.ValueObjects;

namespace SellingNewProduct.Domain.Services;

/// <summary>
/// Implements the payment inbound port. Owns the rules that relate a payment to the order it
/// settles (payable status, matching currency, no overpayment) — logic that needs both the
/// Payment and the Order aggregate and therefore does not belong on either one alone.
/// </summary>
public sealed class PaymentWriteService : IPaymentWriteService
{
    private readonly IPaymentWriteRepository myPaymentRepository;
    private readonly IOrderWriteRepository myOrderRepository;

    public PaymentWriteService(
        IPaymentWriteRepository thePaymentRepository,
        IOrderWriteRepository theOrderRepository)
    {
        myPaymentRepository = thePaymentRepository;
        myOrderRepository = theOrderRepository;
    }

    /// <summary>Records a payment against an order after enforcing the settlement rules.</summary>
    public async Task<Payment> CreateAsync(
        Guid theOrderId,
        Money theAmount,
        PaymentMethod theMethod,
        CancellationToken theCancellationToken = default)
    {
        // A payment must reference an existing order.
        var aOrder = await myOrderRepository.GetByIdAsync(theOrderId, theCancellationToken);
        if (aOrder is null)
        {
            throw new NotFoundException($"Order '{theOrderId}' not found.");
        }

        // Only a real sale can be paid — not a Draft (not placed yet) or a Cancelled order.
        if (aOrder.OrderStatus is not (OrderStatus.Confirmed or OrderStatus.Shipped))
        {
            throw new ConflictException($"Cannot pay an order in status {aOrder.OrderStatus}; it must be Confirmed or Shipped.");
        }

        // The payment must be in the same currency as the order it settles.
        if (theAmount.Currency != aOrder.TotalAmount.Currency)
        {
            throw new ConflictException($"Payment currency {theAmount.Currency} does not match the order currency {aOrder.TotalAmount.Currency}.");
        }

        // Prevent overpayment: completed payments so far + this one may not exceed the order total.
        var aExistingPayments = await myPaymentRepository.GetByOrderAsync(theOrderId, theCancellationToken);
        var aAlreadyPaid = aExistingPayments
            .Where(p => p.PaymentStatus == PaymentStatus.Completed)
            .Sum(p => p.Amount.Amount);

        if (aAlreadyPaid + theAmount.Amount > aOrder.TotalAmount.Amount)
        {
            throw new ConflictException(
                $"Payment exceeds the outstanding balance. Order total {aOrder.TotalAmount.Amount}, already paid {aAlreadyPaid}, attempted {theAmount.Amount}.");
        }

        var aPayment = Payment.Create(theOrderId, theAmount, theMethod);

        await myPaymentRepository.AddAsync(aPayment, theCancellationToken);
        return aPayment;
    }

    /// <summary>Marks an existing payment as completed.</summary>
    public async Task<Payment> CompleteAsync(Guid thePaymentId, CancellationToken theCancellationToken = default)
    {
        var aPayment = await myPaymentRepository.GetByIdAsync(thePaymentId, theCancellationToken);
        if (aPayment is null)
        {
            throw new NotFoundException($"Payment '{thePaymentId}' not found.");
        }

        aPayment.MarkCompleted();
        await myPaymentRepository.UpdateAsync(aPayment, theCancellationToken);
        return aPayment;
    }

    /// <summary>
    /// Completes the pending payment of an order — used by a payment-gateway callback that only knows
    /// the order id. Idempotent by design: a payment already Completed is returned as-is (a duplicate
    /// return/IPN call is a no-op, so our records never double-count), and a missing payment returns
    /// null instead of throwing.
    /// </summary>
    public async Task<Payment?> CompleteByOrderAsync(Guid theOrderId, CancellationToken theCancellationToken = default)
    {
        var aPayments = await myPaymentRepository.GetByOrderAsync(theOrderId, theCancellationToken);

        // Already completed → duplicate callback; return it unchanged (idempotent).
        var aCompleted = aPayments.FirstOrDefault(p => p.PaymentStatus == PaymentStatus.Completed);
        if (aCompleted is not null)
        {
            return aCompleted;
        }

        // Otherwise complete the pending one. No pending payment recorded → nothing to do (null).
        var aPending = aPayments.FirstOrDefault(p => p.PaymentStatus == PaymentStatus.Pending);
        if (aPending is null)
        {
            return null;
        }

        aPending.MarkCompleted();
        await myPaymentRepository.UpdateAsync(aPending, theCancellationToken);
        return aPending;
    }
}
