using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Domain.Abstractions;

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
    /// One enriched product row (with the category name) for display, or <c>null</c> if
    /// not found. The flat read-side counterpart of the repository's <c>GetByIdAsync</c>,
    /// which returns the full aggregate.
    /// </summary>
    Task<ProductSummaryView?> GetByIdAsync(Guid theProductId, CancellationToken theCancellationToken = default);

    /// <summary>
    /// Search/filter the catalogue. Every filter is optional (null = no filter):
    /// <list type="bullet">
    /// <item><paramref name="theName"/>: case-insensitive "contains" on the product name.</item>
    /// <item><paramref name="theCategoryId"/>: product category.</item>
    /// <item><paramref name="thePriceFrom"/>/<paramref name="thePriceTo"/>: price range.</item>
    /// <item><paramref name="theMinStock"/>/<paramref name="theMaxStock"/>: stock-quantity range
    /// (e.g. low-stock = maxStock 5; in-stock = minStock 1).</item>
    /// <item><paramref name="theStatus"/>: Active/Inactive. Soft-deleted rows are never returned.</item>
    /// </list>
    /// <paramref name="theQuery"/>.SortBy accepts <c>name</c>, <c>price</c> or <c>stock</c>
    /// (default <c>name</c>). Returns one page plus the total matching count.
    /// </summary>
    Task<PagedResult<ProductSummaryView>> SearchAsync(
        ProductSearchQuery theQuery,
        CancellationToken theCancellationToken = default);
}
