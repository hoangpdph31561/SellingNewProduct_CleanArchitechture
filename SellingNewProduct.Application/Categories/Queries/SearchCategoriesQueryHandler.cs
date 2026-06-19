using MediatR;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Interfaces.Inbound;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Application.Categories;

public sealed class SearchCategoriesQueryHandler : IRequestHandler<SearchCategoriesQuery, PagedResult<CategorySummaryView>>
{
    private readonly ICategoryReadService myCategoryReadService;

    public SearchCategoriesQueryHandler(ICategoryReadService theCategoryReadService)
    {
        myCategoryReadService = theCategoryReadService;
    }

    public Task<PagedResult<CategorySummaryView>> Handle(SearchCategoriesQuery theQuery, CancellationToken theCancellationToken)
        => myCategoryReadService.SearchAsync(theQuery.Criteria, theCancellationToken);
}
