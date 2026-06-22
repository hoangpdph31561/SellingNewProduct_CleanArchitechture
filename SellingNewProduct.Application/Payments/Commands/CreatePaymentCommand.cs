using FluentValidation;
using MediatR;
using SellingNewProduct.Domain.Interfaces.Inbound;
using SellingNewProduct.Domain.Payments;
using SellingNewProduct.Domain.ValueObjects;

namespace SellingNewProduct.Application.Payments;

/// <summary>Write-side command: record a payment against an order. <c>Method</c> is the PaymentMethod enum value.</summary>
public sealed record CreatePaymentCommand(
    Guid OrderId,
    decimal Amount,
    string Currency,
    int Method) : IRequest<Payment>;

public sealed class CreatePaymentCommandHandler : IRequestHandler<CreatePaymentCommand, Payment>
{
    private readonly IPaymentWriteService myPaymentWriteService;

    public CreatePaymentCommandHandler(IPaymentWriteService thePaymentWriteService)
    {
        myPaymentWriteService = thePaymentWriteService;
    }

    public Task<Payment> Handle(CreatePaymentCommand theCommand, CancellationToken theCancellationToken)
    {
        var aAmount = Money.Create(theCommand.Amount, theCommand.Currency);
        return myPaymentWriteService.CreateAsync(
            theCommand.OrderId, aAmount, (PaymentMethod)theCommand.Method, theCancellationToken);
    }
}

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
