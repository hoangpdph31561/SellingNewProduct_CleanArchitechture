using SellingNewProduct.Domain.Categories;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Interfaces.Inbound;
using SellingNewProduct.Domain.Interfaces.Outbound;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Domain.Services;

/// <summary>Implements the category read port; forwards to the read repository (no business rules).</summary>
public sealed class CategoryReadService : ICategoryReadService
{
    private readonly ICategoryReadRepository myCategoryRepository;

    public CategoryReadService(ICategoryReadRepository theCategoryRepository)
    {
        myCategoryRepository = theCategoryRepository;
    }

    public Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken theCancellationToken = default)
        => myCategoryRepository.GetAllAsync(theCancellationToken);

    public Task<Category?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default)
        => myCategoryRepository.GetByIdAsync(theId, theCancellationToken);

    public Task<IReadOnlyList<CategorySummaryView>> GetSummariesAsync(CancellationToken theCancellationToken = default)
        => myCategoryRepository.GetCategorySummariesAsync(theCancellationToken);

    public Task<PagedResult<CategorySummaryView>> SearchAsync(CategorySearchQuery theCriteria, CancellationToken theCancellationToken = default)
        => myCategoryRepository.SearchAsync(theCriteria, theCancellationToken);
}
