using SellingNewProduct.Domain.Abstractions;
using SellingNewProduct.Domain.Commands;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Orders;
using SellingNewProduct.Domain.Payments;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;
using SellingNewProduct.Domain.Repositories;
using SellingNewProduct.Domain.ValueObjects;

namespace SellingNewProduct.Domain.Services;

internal sealed class PaymentService : IPaymentService
{
    private readonly IPaymentRepository myPaymentRepository;
    private readonly IOrderRepository myOrderRepository;

    public PaymentService(IPaymentRepository thePaymentRepository, IOrderRepository theOrderRepository)
    {
        myPaymentRepository = thePaymentRepository;
        myOrderRepository = theOrderRepository;
    }

    public Task<Payment?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default)
        => myPaymentRepository.GetByIdAsync(theId, theCancellationToken);

    public async Task<Payment> CreateAsync(CreatePaymentCommand theCommand, CancellationToken theCancellationToken = default)
    {
        // A payment must reference an existing order.
        var aOrder = await myOrderRepository.GetByIdAsync(theCommand.OrderId, theCancellationToken);
        if (aOrder is null)
        {
            throw new NotFoundException($"Order '{theCommand.OrderId}' not found.");
        }

        // Only a real sale can be paid — not a Draft (not placed yet) or a Cancelled order.
        if (aOrder.OrderStatus is not (OrderStatus.Confirmed or OrderStatus.Shipped))
        {
            throw new ConflictException($"Cannot pay an order in status {aOrder.OrderStatus}; it must be Confirmed or Shipped.");
        }

        var aAmount = Money.Create(theCommand.Amount, theCommand.Currency);

        // The payment must be in the same currency as the order it settles.
        if (aAmount.Currency != aOrder.TotalAmount.Currency)
        {
            throw new ConflictException($"Payment currency {aAmount.Currency} does not match the order currency {aOrder.TotalAmount.Currency}.");
        }

        // Prevent overpayment: completed payments so far + this one may not exceed the order total.
        var aExistingPayments = await myPaymentRepository.GetByOrderAsync(theCommand.OrderId, theCancellationToken);
        var aAlreadyPaid = aExistingPayments
            .Where(p => p.PaymentStatus == PaymentStatus.Completed)
            .Sum(p => p.Amount.Amount);

        if (aAlreadyPaid + aAmount.Amount > aOrder.TotalAmount.Amount)
        {
            throw new ConflictException(
                $"Payment exceeds the outstanding balance. Order total {aOrder.TotalAmount.Amount}, already paid {aAlreadyPaid}, attempted {aAmount.Amount}.");
        }

        var aPayment = Payment.Create(theCommand.OrderId, aAmount, (PaymentMethod)theCommand.Method);

        await myPaymentRepository.AddAsync(aPayment, theCancellationToken);
        return aPayment;
    }

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

    public Task<PagedResult<PaymentSummaryView>> SearchAsync(PaymentSearchQuery theQuery, CancellationToken theCancellationToken = default)
        => myPaymentRepository.SearchAsync(theQuery, theCancellationToken);

    public Task<PagedResult<OutstandingOrderView>> GetOutstandingOrdersAsync(int thePage = 1, int thePageSize = PageRequest.DefaultPageSize, CancellationToken theCancellationToken = default)
        => myPaymentRepository.GetOutstandingOrdersAsync(thePage, thePageSize, theCancellationToken);
}
