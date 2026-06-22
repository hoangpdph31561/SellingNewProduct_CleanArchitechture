using MediatR;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Interfaces.Inbound;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Application.Products;

/// <summary>Wraps the catalogue search criteria (filters/paging/sorting) as a MediatR query.</summary>
public sealed record SearchProductsQuery(ProductSearchQuery Criteria) : IRequest<PagedResult<ProductSummaryView>>;

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
