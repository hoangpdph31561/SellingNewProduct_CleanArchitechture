using MediatR;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Interfaces.Inbound;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Application.Products;

public sealed class SearchProductsQueryHandler : IRequestHandler<SearchProductsQuery, PagedResult<ProductSummaryView>>
{
    private readonly IProductReadService myProductReadService;

    public SearchProductsQueryHandler(IProductReadService theProductReadService)
    {
        myProductReadService = theProductReadService;
    }

    public Task<PagedResult<ProductSummaryView>> Handle(SearchProductsQuery theQuery, CancellationToken theCancellationToken)
        => myProductReadService.SearchAsync(theQuery.Criteria, theCancellationToken);
}
