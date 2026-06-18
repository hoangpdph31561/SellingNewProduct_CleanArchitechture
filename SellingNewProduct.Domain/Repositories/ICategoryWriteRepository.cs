using SellingNewProduct.Domain.Categories;

namespace SellingNewProduct.Domain.Repositories;

public interface ICategoryWriteRepository
{
    /// <summary>Loads a category aggregate (used by the product-create rule "category must exist").</summary>
    Task<Category?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default);

    /// <summary>True if a category with the same name already exists (for the "unique name" rule).</summary>
    Task<bool> ExistsByNameAsync(string theName, CancellationToken theCancellationToken = default);

    Task AddAsync(Category theCategory, CancellationToken theCancellationToken = default);

    Task UpdateAsync(Category theCategory, CancellationToken theCancellationToken = default);
}
