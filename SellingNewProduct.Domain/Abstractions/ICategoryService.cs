using SellingNewProduct.Domain.Categories;
using SellingNewProduct.Domain.Commands;

namespace SellingNewProduct.Domain.Abstractions;

/// <summary>
/// Category business behavior. This is the ONLY thing the API layer depends on —
/// it calls a method without knowing which repository/DB sits underneath.
/// Business rules (e.g. unique name) live in the implementation, not the controller.
/// </summary>
public interface ICategoryService
{
    Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken theCancellationToken = default);

    Task<Category?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default);

    Task<Category> CreateAsync(CreateCategoryCommand theCommand, CancellationToken theCancellationToken = default);
}
