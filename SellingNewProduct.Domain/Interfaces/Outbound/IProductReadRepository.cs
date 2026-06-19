using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Products;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Domain.Interfaces.Outbound;

/// <summary>
/// Read side for the product catalogue: queries that feed the GET endpoints. Returns either
/// the aggregate (for "show one / list all") or flat read models (search/summary). Never
/// mutates state. Plugged in by each infrastructure project's read repository.
/// </summary>
public interface IProductReadRepository : IReadRepository<Product>
{
    Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken theCancellationToken = default);

    Task<IReadOnlyList<Product>> GetByCategoryAsync(Guid theCategoryId, CancellationToken theCancellationToken = default);

    Task<ProductSummaryView?> GetSummaryByIdAsync(Guid theProductId, CancellationToken theCancellationToken = default);

    Task<PagedResult<ProductSummaryView>> SearchAsync(ProductSearchQuery theQuery, CancellationToken theCancellationToken = default);
}
