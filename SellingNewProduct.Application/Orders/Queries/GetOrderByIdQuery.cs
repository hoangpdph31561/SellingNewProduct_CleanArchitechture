using MediatR;
using SellingNewProduct.Domain.Interfaces.Inbound;
using SellingNewProduct.Domain.Orders;

namespace SellingNewProduct.Application.Orders;

public sealed record GetOrderByIdQuery(Guid Id) : IRequest<Order?>;

public sealed class GetOrderByIdQueryHandler : IRequestHandler<GetOrderByIdQuery, Order?>
{
    private readonly IOrderReadService myOrderReadService;

    public GetOrderByIdQueryHandler(IOrderReadService theOrderReadService)
    {
        myOrderReadService = theOrderReadService;
    }

    public Task<Order?> Handle(GetOrderByIdQuery theQuery, CancellationToken theCancellationToken)
        => myOrderReadService.GetByIdAsync(theQuery.Id, theCancellationToken);
}
