using MediatR;
using SellingNewProduct.Domain.Interfaces.Inbound;
using SellingNewProduct.Domain.Orders;

namespace SellingNewProduct.Application.Orders;

public sealed class ShipOrderCommandHandler : IRequestHandler<ShipOrderCommand, Order>
{
    private readonly IOrderWriteService myOrderWriteService;

    public ShipOrderCommandHandler(IOrderWriteService theOrderWriteService)
    {
        myOrderWriteService = theOrderWriteService;
    }

    public Task<Order> Handle(ShipOrderCommand theCommand, CancellationToken theCancellationToken) =>
        myOrderWriteService.ShipAsync(theCommand.Id, theCancellationToken);
}
