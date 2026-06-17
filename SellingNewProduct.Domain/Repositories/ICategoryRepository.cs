using SellingNewProduct.Domain.Categories;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Domain.Repositories;

public interface ICategoryRepository
{
    Task<Category?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default);

    Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken theCancellationToken = default);

    /// <summary>True if a category with the same name already exists (for the "unique name" rule).</summary>
    Task<bool> ExistsByNameAsync(string theName, CancellationToken theCancellationToken = default);

    Task AddAsync(Category theCategory, CancellationToken theCancellationToken = default);

    Task UpdateAsync(Category theCategory, CancellationToken theCancellationToken = default);

    // Read side: category summaries enriched with product count and stock value. These return
    // read models (not the aggregate) — a JOIN/GROUP BY that does not belong on Category itself.
    Task<IReadOnlyList<CategorySummaryView>> GetCategorySummariesAsync(CancellationToken theCancellationToken = default);

    Task<PagedResult<CategorySummaryView>> SearchAsync(CategorySearchQuery theQuery, CancellationToken theCancellationToken = default);
}
