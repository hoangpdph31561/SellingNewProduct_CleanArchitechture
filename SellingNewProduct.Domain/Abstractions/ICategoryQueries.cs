using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Domain.Abstractions;

/// <summary>
/// Read side for categories — kept separate from <c>ICategoryRepository</c> (write side).
/// Each row is enriched with how many products the category contains and the total value
/// of their stock — a JOIN + GROUP BY that does NOT belong on the Category aggregate.
/// </summary>
public interface ICategoryQueries
{
    /// <summary>
    /// Every category with its product count and total stock value, ordered by name.
    /// (SQL: Categories LEFT JOIN Products, GROUP BY category.) The list is small by
    /// nature, so this is not paged.
    /// </summary>
    Task<IReadOnlyList<CategorySummaryView>> GetCategorySummariesAsync(CancellationToken theCancellationToken = default);

    /// <summary>
    /// Search categories — see <see cref="CategorySearchQuery"/> for the name filter and
    /// paging. Each row is enriched with product count and stock value, ordered by name.
    /// Returns one page plus the total count.
    /// </summary>
    Task<PagedResult<CategorySummaryView>> SearchAsync(CategorySearchQuery theQuery, CancellationToken theCancellationToken = default);
}
