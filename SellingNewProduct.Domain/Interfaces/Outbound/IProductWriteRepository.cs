using SellingNewProduct.Domain.Products;

namespace SellingNewProduct.Domain.Interfaces.Outbound;

/// <summary>
/// Write side for the product aggregate: load-to-mutate plus persistence. Used by command
/// handlers (create products) and by the order flow (load by ids, adjust stock in bulk).
/// </summary>
public interface IProductWriteRepository : IWriteRepository<Product>
{
    /// <summary>Loads several products by id in one round-trip (for the order flow's stock handling).</summary>
    Task<IReadOnlyList<Product>> GetByIdsAsync(IReadOnlyCollection<Guid> theIds, CancellationToken theCancellationToken = default);

    /// <summary>True if a product with the same SKU already exists (for the "unique SKU" rule).</summary>
    Task<bool> ExistsBySkuAsync(string theSku, CancellationToken theCancellationToken = default);

    /// <summary>
    /// Returns the subset of <paramref name="theSkus"/> that already exist, in ONE round-trip.
    /// Used by bulk create to enforce the unique-SKU rule without firing one query per SKU
    /// (an N+1) — which also cannot be parallelized, since the calls share one DbContext.
    /// </summary>
    Task<IReadOnlyCollection<string>> GetExistingSkusAsync(IReadOnlyCollection<string> theSkus, CancellationToken theCancellationToken = default);

    /// <summary>Inserts several products in a single SaveChanges (bulk create).</summary>
    Task AddRangeAsync(IEnumerable<Product> theProducts, CancellationToken theCancellationToken = default);

    /// <summary>Persists changes to several products in a single SaveChanges (e.g. stock adjustments).</summary>
    Task UpdateRangeAsync(IEnumerable<Product> theProducts, CancellationToken theCancellationToken = default);
}
