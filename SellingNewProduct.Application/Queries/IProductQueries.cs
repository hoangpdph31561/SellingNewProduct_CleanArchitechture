using SellingNewProduct.Application.Common;
using SellingNewProduct.Application.ReadModels;
using SellingNewProduct.Domain.Common;

namespace SellingNewProduct.Application.Queries;

/// <summary>
/// Read side for the product catalogue — a list/search screen, separate from
/// <c>IProductRepository</c> (write side). The repository returns the
/// <see cref="Domain.Products.Product"/> aggregate to run business logic; this
/// returns flat rows already enriched with the category name, filtered, sorted and
/// paginated for display.
///
/// Sorting is expressed as a column name + direction (not a LINQ expression) so the
/// contract stays storage-agnostic: SQL turns it into ORDER BY, Mongo into its own
/// ordering. Unknown <paramref name="theSortBy"/> values fall back to a safe default.
/// </summary>
public interface IProductQueries
{
    /// <summary>
    /// Search/filter the catalogue. Every filter is optional (null = no filter):
    /// <list type="bullet">
    /// <item><paramref name="theName"/>: case-insensitive "contains" on the product name.</item>
    /// <item><paramref name="theCategoryId"/>: loại hàng (category).</item>
    /// <item><paramref name="thePriceFrom"/>/<paramref name="thePriceTo"/>: price range.</item>
    /// <item><paramref name="theMinStock"/>/<paramref name="theMaxStock"/>: stock-quantity range
    /// (e.g. low-stock = maxStock 5; in-stock = minStock 1).</item>
    /// <item><paramref name="theStatus"/>: Active/Inactive. Soft-deleted rows are never returned.</item>
    /// </list>
    /// <paramref name="theSortBy"/> accepts <c>name</c>, <c>price</c> or <c>stock</c>
    /// (default <c>name</c>). Returns one page plus the total matching count.
    /// </summary>
    Task<PagedResult<ProductSummaryView>> SearchAsync(
        string? theName = null,
        Guid? theCategoryId = null,
        decimal? thePriceFrom = null,
        decimal? thePriceTo = null,
        int? theMinStock = null,
        int? theMaxStock = null,
        EntityStatus? theStatus = null,
        int thePage = 1,
        int thePageSize = PageRequest.DefaultPageSize,
        string? theSortBy = null,
        bool theSortDescending = false,
        CancellationToken theCancellationToken = default);
}
