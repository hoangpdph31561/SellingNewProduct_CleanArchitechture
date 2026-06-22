using SellingNewProduct.Domain.Categories;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Interfaces.Inbound;
using SellingNewProduct.Domain.Interfaces.Outbound;

namespace SellingNewProduct.Domain.Services;

/// <summary>
/// Implements the category inbound port. Owns the "unique name" rule, which the Category
/// aggregate cannot check on its own because it requires querying the store.
/// </summary>
public sealed class CategoryWriteService : ICategoryWriteService
{
    private readonly ICategoryWriteRepository myCategoryRepository;

    public CategoryWriteService(ICategoryWriteRepository theCategoryRepository)
    {
        myCategoryRepository = theCategoryRepository;
    }

    /// <summary>Creates a category after enforcing the unique-name rule.</summary>
    public async Task<Category> CreateAsync(
        string theName, string theDescription, CancellationToken theCancellationToken = default)
    {
        // Create (the aggregate validates + trims) then use the normalized name for the uniqueness check.
        var aCategory = Category.Create(theName, theDescription);

        if (await myCategoryRepository.ExistsByNameAsync(aCategory.Name, theCancellationToken))
        {
            throw new ConflictException($"Category '{aCategory.Name}' already exists.");
        }

        await myCategoryRepository.AddAsync(aCategory, theCancellationToken);
        return aCategory;
    }
}
