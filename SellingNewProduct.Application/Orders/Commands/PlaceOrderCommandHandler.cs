using MediatR;
using SellingNewProduct.Domain.Interfaces.Inbound;
using SellingNewProduct.Domain.Orders;
using SellingNewProduct.Domain.ValueObjects;

namespace SellingNewProduct.Application.Orders;

public sealed class PlaceOrderCommandHandler : IRequestHandler<PlaceOrderCommand, Order>
{
    private readonly IOrderWriteService myOrderWriteService;

    public PlaceOrderCommandHandler(IOrderWriteService theOrderWriteService)
    {
        myOrderWriteService = theOrderWriteService;
    }

    public Task<Order> Handle(PlaceOrderCommand theCommand, CancellationToken theCancellationToken)
    {
        var aShippingAddress = Address.Create(
            theCommand.Street, theCommand.Ward, theCommand.District, theCommand.City, theCommand.Country);

        var aLines = theCommand.Items
            .Select(i => new OrderLine(i.ProductId, i.Quantity))
            .ToList();

        return myOrderWriteService.PlaceAsync(
            theCommand.CustomerId, theCommand.EmployeeId, aShippingAddress, aLines, theCancellationToken);
    }
}
