using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Products;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Domain.Repositories;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default);

    /// <summary>Loads several products by id in one round-trip (for the order flow's stock handling).</summary>
    Task<IReadOnlyList<Product>> GetByIdsAsync(IReadOnlyCollection<Guid> theIds, CancellationToken theCancellationToken = default);

    Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken theCancellationToken = default);

    Task<IReadOnlyList<Product>> GetByCategoryAsync(Guid theCategoryId, CancellationToken theCancellationToken = default);

    /// <summary>True if a product with the same SKU already exists (for the "unique SKU" rule).</summary>
    Task<bool> ExistsBySkuAsync(string theSku, CancellationToken theCancellationToken = default);

    Task AddAsync(Product theProduct, CancellationToken theCancellationToken = default);

    /// <summary>Inserts several products in a single SaveChanges (bulk create).</summary>
    Task AddRangeAsync(IEnumerable<Product> theProducts, CancellationToken theCancellationToken = default);

    Task UpdateAsync(Product theProduct, CancellationToken theCancellationToken = default);

    /// <summary>Persists changes to several products in a single SaveChanges (e.g. stock adjustments).</summary>
    Task UpdateRangeAsync(IEnumerable<Product> theProducts, CancellationToken theCancellationToken = default);

    // Read side: flat catalogue rows enriched with the category name (read models, not the aggregate).
    Task<ProductSummaryView?> GetSummaryByIdAsync(Guid theProductId, CancellationToken theCancellationToken = default);

    Task<PagedResult<ProductSummaryView>> SearchAsync(ProductSearchQuery theQuery, CancellationToken theCancellationToken = default);
}
