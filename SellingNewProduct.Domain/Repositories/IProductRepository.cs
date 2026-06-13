using SellingNewProduct.Domain.Products;

namespace SellingNewProduct.Domain.Repositories;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default);

    Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken theCancellationToken = default);

    Task<IReadOnlyList<Product>> GetByCategoryAsync(Guid theCategoryId, CancellationToken theCancellationToken = default);

    Task AddAsync(Product theProduct, CancellationToken theCancellationToken = default);

    Task UpdateAsync(Product theProduct, CancellationToken theCancellationToken = default);
}
