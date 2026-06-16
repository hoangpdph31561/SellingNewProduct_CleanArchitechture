using SellingNewProduct.Domain.Commands;
using SellingNewProduct.Domain.Products;

namespace SellingNewProduct.Domain.Abstractions;

/// <summary>
/// Product write-side behavior. The API depends on this, not on the repositories.
/// (Search/list is read side — see <c>IProductQueries</c>.)
/// </summary>
public interface IProductService
{
    Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken theCancellationToken = default);

    Task<Product?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default);

    Task<Product> CreateAsync(CreateProductCommand theCommand, CancellationToken theCancellationToken = default);
}
