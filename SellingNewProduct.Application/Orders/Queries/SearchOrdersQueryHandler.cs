using MediatR;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Interfaces.Inbound;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Application.Orders;

public sealed class SearchOrdersQueryHandler : IRequestHandler<SearchOrdersQuery, PagedResult<OrderSummaryView>>
{
    private readonly IOrderReadService myOrderReadService;

    public SearchOrdersQueryHandler(IOrderReadService theOrderReadService)
    {
        myOrderReadService = theOrderReadService;
    }

    public Task<PagedResult<OrderSummaryView>> Handle(SearchOrdersQuery theQuery, CancellationToken theCancellationToken)
        => myOrderReadService.SearchAsync(theQuery.Criteria, theCancellationToken);
}
