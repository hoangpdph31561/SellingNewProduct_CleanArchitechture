using FluentValidation;
using MediatR;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Orders;
using SellingNewProduct.Domain.Payments;
using SellingNewProduct.Domain.Repositories;
using SellingNewProduct.Domain.ValueObjects;

namespace SellingNewProduct.Application.Payments;

// ----- Create payment -----------------------------------------------------------------------

/// <summary>Write-side command: record a payment against an order. <c>Method</c> is the PaymentMethod enum value.</summary>
public sealed record CreatePaymentCommand(
    Guid OrderId,
    decimal Amount,
    string Currency,
    int Method) : IRequest<Payment>;

public sealed class CreatePaymentCommandValidator : AbstractValidator<CreatePaymentCommand>
{
    public CreatePaymentCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Currency).NotEmpty().Length(3);
        RuleFor(x => x.Method).InclusiveBetween(1, 4);
    }
}

public sealed class CreatePaymentCommandHandler : IRequestHandler<CreatePaymentCommand, Payment>
{
    private readonly IPaymentWriteRepository myPaymentRepository;
    private readonly IOrderWriteRepository myOrderRepository;

    public CreatePaymentCommandHandler(
        IPaymentWriteRepository thePaymentRepository,
        IOrderWriteRepository theOrderRepository)
    {
        myPaymentRepository = thePaymentRepository;
        myOrderRepository = theOrderRepository;
    }

    public async Task<Payment> Handle(CreatePaymentCommand theCommand, CancellationToken theCancellationToken)
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
}

// ----- Complete payment ---------------------------------------------------------------------

public sealed record CompletePaymentCommand(Guid Id) : IRequest<Payment>;

public sealed class CompletePaymentCommandHandler : IRequestHandler<CompletePaymentCommand, Payment>
{
    private readonly IPaymentWriteRepository myPaymentRepository;

    public CompletePaymentCommandHandler(IPaymentWriteRepository thePaymentRepository)
    {
        myPaymentRepository = thePaymentRepository;
    }

    public async Task<Payment> Handle(CompletePaymentCommand theCommand, CancellationToken theCancellationToken)
    {
        var aPayment = await myPaymentRepository.GetByIdAsync(theCommand.Id, theCancellationToken);
        if (aPayment is null)
        {
            throw new NotFoundException($"Payment '{theCommand.Id}' not found.");
        }

        aPayment.MarkCompleted();
        await myPaymentRepository.UpdateAsync(aPayment, theCancellationToken);
        return aPayment;
    }
}
