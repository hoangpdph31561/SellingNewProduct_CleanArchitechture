using SellingNewProduct.Domain.Categories;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Domain.Repositories;

public interface ICategoryReadRepository
{
    Task<Category?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default);

    Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken theCancellationToken = default);

    Task<IReadOnlyList<CategorySummaryView>> GetCategorySummariesAsync(CancellationToken theCancellationToken = default);

    Task<PagedResult<CategorySummaryView>> SearchAsync(CategorySearchQuery theQuery, CancellationToken theCancellationToken = default);
}
