using MediatR;
using SellingNewProduct.Domain.Categories;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;
using SellingNewProduct.Domain.Repositories;

namespace SellingNewProduct.Application.Categories;

public sealed record GetAllCategoriesQuery : IRequest<IReadOnlyList<Category>>;

public sealed record GetCategoryByIdQuery(Guid Id) : IRequest<Category?>;

public sealed record GetCategorySummariesQuery : IRequest<IReadOnlyList<CategorySummaryView>>;

public sealed record SearchCategoriesQuery(CategorySearchQuery Criteria) : IRequest<PagedResult<CategorySummaryView>>;

public sealed class CategoryQueryHandlers :
    IRequestHandler<GetAllCategoriesQuery, IReadOnlyList<Category>>,
    IRequestHandler<GetCategoryByIdQuery, Category?>,
    IRequestHandler<GetCategorySummariesQuery, IReadOnlyList<CategorySummaryView>>,
    IRequestHandler<SearchCategoriesQuery, PagedResult<CategorySummaryView>>
{
    private readonly ICategoryReadRepository myCategoryRepository;

    public CategoryQueryHandlers(ICategoryReadRepository theCategoryRepository)
    {
        myCategoryRepository = theCategoryRepository;
    }

    public Task<IReadOnlyList<Category>> Handle(GetAllCategoriesQuery theQuery, CancellationToken theCancellationToken)
        => myCategoryRepository.GetAllAsync(theCancellationToken);

    public Task<Category?> Handle(GetCategoryByIdQuery theQuery, CancellationToken theCancellationToken)
        => myCategoryRepository.GetByIdAsync(theQuery.Id, theCancellationToken);

    public Task<IReadOnlyList<CategorySummaryView>> Handle(GetCategorySummariesQuery theQuery, CancellationToken theCancellationToken)
        => myCategoryRepository.GetCategorySummariesAsync(theCancellationToken);

    public Task<PagedResult<CategorySummaryView>> Handle(SearchCategoriesQuery theQuery, CancellationToken theCancellationToken)
        => myCategoryRepository.SearchAsync(theQuery.Criteria, theCancellationToken);
}
