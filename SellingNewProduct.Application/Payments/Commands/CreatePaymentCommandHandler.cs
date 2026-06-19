using MediatR;
using SellingNewProduct.Domain.Interfaces.Inbound;
using SellingNewProduct.Domain.Payments;
using SellingNewProduct.Domain.ValueObjects;

namespace SellingNewProduct.Application.Payments;

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
