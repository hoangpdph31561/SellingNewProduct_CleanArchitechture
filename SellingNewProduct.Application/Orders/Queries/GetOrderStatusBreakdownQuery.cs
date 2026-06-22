using MediatR;
using SellingNewProduct.Domain.Interfaces.Inbound;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Application.Orders;

public sealed record GetOrderStatusBreakdownQuery : IRequest<IReadOnlyList<OrderStatusCountView>>;

public sealed class GetOrderStatusBreakdownQueryHandler : IRequestHandler<GetOrderStatusBreakdownQuery, IReadOnlyList<OrderStatusCountView>>
{
    private readonly IOrderReadService myOrderReadService;

    public GetOrderStatusBreakdownQueryHandler(IOrderReadService theOrderReadService)
    {
        myOrderReadService = theOrderReadService;
    }

    public Task<IReadOnlyList<OrderStatusCountView>> Handle(GetOrderStatusBreakdownQuery theQuery, CancellationToken theCancellationToken)
        => myOrderReadService.GetStatusBreakdownAsync(theCancellationToken);
}
