using SellingNewProduct.Domain.Abstractions;
using SellingNewProduct.Domain.Commands;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Payments;
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

        var aPayment = Payment.Create(
            theCommand.OrderId,
            Money.Create(theCommand.Amount, theCommand.Currency),
            (PaymentMethod)theCommand.Method);

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
}
