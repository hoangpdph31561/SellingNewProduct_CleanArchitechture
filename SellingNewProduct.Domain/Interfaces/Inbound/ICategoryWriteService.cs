using SellingNewProduct.Domain.Categories;

namespace SellingNewProduct.Domain.Interfaces.Inbound;

/// <summary>
/// Inbound (driving) port for category creation. Owns the "unique name" rule, which the
/// Category aggregate cannot check on its own because it requires querying the store.
/// </summary>
public interface ICategoryWriteService : IWriteService<Category>
{
    /// <summary>Creates a category after enforcing the unique-name rule.</summary>
    Task<Category> CreateAsync(
        string theName, string theDescription, CancellationToken theCancellationToken = default);
}
