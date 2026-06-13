using SellingNewProduct.Domain.Categories;

namespace SellingNewProduct.Domain.Repositories;

public interface ICategoryRepository
{
    Task<Category?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default);

    Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken theCancellationToken = default);

    Task AddAsync(Category theCategory, CancellationToken theCancellationToken = default);

    Task UpdateAsync(Category theCategory, CancellationToken theCancellationToken = default);
}
