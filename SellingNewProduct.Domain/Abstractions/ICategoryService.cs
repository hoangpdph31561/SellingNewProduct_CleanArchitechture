using SellingNewProduct.Domain.Categories;
using SellingNewProduct.Domain.Commands;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Domain.Abstractions;

/// <summary>
/// Category business behavior. This is the ONLY thing the API layer depends on —
/// it calls a method without knowing which repository/DB sits underneath.
/// Business rules (e.g. unique name) live in the implementation, not the controller.
/// Read-side projections (summaries/search) are exposed here too so the API has a
/// single entry point per module; the heavy lifting still happens in the repository.
/// </summary>
public interface ICategoryService
{
    Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken theCancellationToken = default);

    Task<Category?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default);

    Task<Category> CreateAsync(CreateCategoryCommand theCommand, CancellationToken theCancellationToken = default);

    /// <summary>Every category enriched with its product count and total stock value, ordered by name.</summary>
    Task<IReadOnlyList<CategorySummaryView>> GetCategorySummariesAsync(CancellationToken theCancellationToken = default);

    /// <summary>Search categories (see <see cref="CategorySearchQuery"/>), one page of enriched rows plus the total count.</summary>
    Task<PagedResult<CategorySummaryView>> SearchAsync(CategorySearchQuery theQuery, CancellationToken theCancellationToken = default);
}
