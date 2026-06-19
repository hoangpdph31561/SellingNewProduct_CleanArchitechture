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
}
