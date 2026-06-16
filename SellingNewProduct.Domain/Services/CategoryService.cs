using SellingNewProduct.Domain.Abstractions;
using SellingNewProduct.Domain.Categories;
using SellingNewProduct.Domain.Commands;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Repositories;

namespace SellingNewProduct.Domain.Services;

/// <summary>
/// Category behavior implementation. Coordinates the <see cref="Category"/> aggregate
/// and <see cref="ICategoryRepository"/> (interface owned by Domain, plugged in by Infra).
/// <c>internal</c>: the outside world only sees <see cref="ICategoryService"/>.
/// </summary>
internal sealed class CategoryService : ICategoryService
{
    private readonly ICategoryRepository myCategoryRepository;

    public CategoryService(ICategoryRepository theCategoryRepository)
    {
        myCategoryRepository = theCategoryRepository;
    }

    public Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken theCancellationToken = default)
        => myCategoryRepository.GetAllAsync(theCancellationToken);

    public Task<Category?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default)
        => myCategoryRepository.GetByIdAsync(theId, theCancellationToken);

    public async Task<Category> CreateAsync(CreateCategoryCommand theCommand, CancellationToken theCancellationToken = default)
    {
        // Create (the aggregate validates + trims) then use the normalized name for the uniqueness check.
        var aCategory = Category.Create(theCommand.Name, theCommand.Description);

        if (await myCategoryRepository.ExistsByNameAsync(aCategory.Name, theCancellationToken))
        {
            // Uniqueness conflict → 409 (mapped in the API), not a plain 400.
            throw new ConflictException($"Category '{aCategory.Name}' already exists.");
        }

        await myCategoryRepository.AddAsync(aCategory, theCancellationToken);
        return aCategory;
    }
}
