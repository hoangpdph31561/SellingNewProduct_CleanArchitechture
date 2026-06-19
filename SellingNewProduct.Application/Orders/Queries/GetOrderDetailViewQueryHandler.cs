using MediatR;
using SellingNewProduct.Domain.Interfaces.Inbound;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Application.Orders;

public sealed class GetOrderDetailViewQueryHandler : IRequestHandler<GetOrderDetailViewQuery, OrderDetailView?>
{
    private readonly IOrderReadService myOrderReadService;

    public GetOrderDetailViewQueryHandler(IOrderReadService theOrderReadService)
    {
        myOrderReadService = theOrderReadService;
    }

    public Task<OrderDetailView?> Handle(GetOrderDetailViewQuery theQuery, CancellationToken theCancellationToken)
        => myOrderReadService.GetDetailAsync(theQuery.Id, theCancellationToken);
}
