using MediatR;
using SellingNewProduct.Domain.Interfaces.Inbound;
using SellingNewProduct.Domain.Orders;

namespace SellingNewProduct.Application.Orders;

public sealed record CancelOrderCommand(Guid Id) : IRequest<Order>;

public sealed class CancelOrderCommandHandler : IRequestHandler<CancelOrderCommand, Order>
{
    private readonly IOrderWriteService myOrderWriteService;

    public CancelOrderCommandHandler(IOrderWriteService theOrderWriteService)
    {
        myOrderWriteService = theOrderWriteService;
    }

    public Task<Order> Handle(CancelOrderCommand theCommand, CancellationToken theCancellationToken) =>
        myOrderWriteService.CancelAsync(theCommand.Id, theCancellationToken);
}
