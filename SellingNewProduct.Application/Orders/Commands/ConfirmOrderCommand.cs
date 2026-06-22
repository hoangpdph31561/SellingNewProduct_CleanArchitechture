using MediatR;
using SellingNewProduct.Domain.Interfaces.Inbound;
using SellingNewProduct.Domain.Orders;

namespace SellingNewProduct.Application.Orders;

public sealed record ConfirmOrderCommand(Guid Id) : IRequest<Order>;

public sealed class ConfirmOrderCommandHandler : IRequestHandler<ConfirmOrderCommand, Order>
{
    private readonly IOrderWriteService myOrderWriteService;

    public ConfirmOrderCommandHandler(IOrderWriteService theOrderWriteService)
    {
        myOrderWriteService = theOrderWriteService;
    }

    public Task<Order> Handle(ConfirmOrderCommand theCommand, CancellationToken theCancellationToken) =>
        myOrderWriteService.ConfirmAsync(theCommand.Id, theCancellationToken);
}
