using SellingNewProduct.Domain.Products;

namespace SellingNewProduct.Domain.Interfaces.Inbound;

/// <summary>
/// Inbound (driving) port for product creation. Owns the cross-aggregate rules: the referenced
/// category must exist and the SKU must be unique (within a batch and against the store).
/// </summary>
public interface IProductWriteService : IWriteService<Product>
{
    /// <summary>Creates a single product after enforcing the category and unique-SKU rules.</summary>
    Task<Product> CreateAsync(NewProduct theRequest, CancellationToken theCancellationToken = default);

    /// <summary>Bulk create. The unique-SKU rule is enforced within the batch and against the store.</summary>
    Task<IReadOnlyList<Product>> CreateManyAsync(
        IReadOnlyList<NewProduct> theRequests, CancellationToken theCancellationToken = default);
}
