using MediatR;
using SellingNewProduct.Domain.Interfaces.Inbound;
using SellingNewProduct.Domain.Payments;

namespace SellingNewProduct.Application.Payments;

public sealed record CompletePaymentCommand(Guid Id) : IRequest<Payment>;

public sealed class CompletePaymentCommandHandler : IRequestHandler<CompletePaymentCommand, Payment>
{
    private readonly IPaymentWriteService myPaymentWriteService;

    public CompletePaymentCommandHandler(IPaymentWriteService thePaymentWriteService)
    {
        myPaymentWriteService = thePaymentWriteService;
    }

    public Task<Payment> Handle(CompletePaymentCommand theCommand, CancellationToken theCancellationToken) =>
        myPaymentWriteService.CompleteAsync(theCommand.Id, theCancellationToken);
}
