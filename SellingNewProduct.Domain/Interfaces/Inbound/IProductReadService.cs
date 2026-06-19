using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Products;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Domain.Interfaces.Inbound;

/// <summary>Inbound (driving) port for product reads.</summary>
public interface IProductReadService : IReadService<Product>
{
    Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken theCancellationToken = default);

    Task<ProductSummaryView?> GetSummaryByIdAsync(Guid theId, CancellationToken theCancellationToken = default);

    Task<PagedResult<ProductSummaryView>> SearchAsync(ProductSearchQuery theCriteria, CancellationToken theCancellationToken = default);
}
