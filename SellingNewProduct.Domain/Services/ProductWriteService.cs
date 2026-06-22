using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Interfaces.Inbound;
using SellingNewProduct.Domain.Interfaces.Outbound;
using SellingNewProduct.Domain.Products;
using SellingNewProduct.Domain.ValueObjects;

namespace SellingNewProduct.Domain.Services;

/// <summary>
/// Implements the product inbound port. Owns the cross-aggregate rules — the referenced
/// category must exist and the SKU must be unique (both within a bulk batch and against the
/// store) — that the Product aggregate cannot enforce on its own.
/// </summary>
public sealed class ProductWriteService : IProductWriteService
{
    private readonly IProductWriteRepository myProductRepository;
    private readonly ICategoryWriteRepository myCategoryRepository;

    public ProductWriteService(
        IProductWriteRepository theProductRepository,
        ICategoryWriteRepository theCategoryRepository)
    {
        myProductRepository = theProductRepository;
        myCategoryRepository = theCategoryRepository;
    }

    /// <summary>Creates a single product after enforcing the category and unique-SKU rules.</summary>
    public async Task<Product> CreateAsync(NewProduct theRequest, CancellationToken theCancellationToken = default)
    {
        await EnsureCategoryExistsAsync(theRequest.CategoryId, theCancellationToken);

        // Normalize the SKU through its value object, then enforce the "unique SKU" business rule.
        var aSku = Sku.Create(theRequest.Sku);
        if (await myProductRepository.ExistsBySkuAsync(aSku.Value, theCancellationToken))
        {
            throw new ConflictException($"A product with SKU '{aSku.Value}' already exists.");
        }

        var aProduct = Build(theRequest, aSku);

        await myProductRepository.AddAsync(aProduct, theCancellationToken);
        return aProduct;
    }

    /// <summary>Bulk create. The unique-SKU rule is enforced within the batch and against the store.</summary>
    public async Task<IReadOnlyList<Product>> CreateManyAsync(
        IReadOnlyList<NewProduct> theRequests, CancellationToken theCancellationToken = default)
    {
        if (theRequests is null || theRequests.Count == 0)
        {
            throw new DomainException("At least one product is required.");
        }

        // Normalize all SKUs first; reject duplicates WITHIN the batch before touching the database.
        var aSkuByIndex = theRequests.Select(r => Sku.Create(r.Sku)).ToList();
        var aDuplicateInBatch = aSkuByIndex.GroupBy(s => s.Value).FirstOrDefault(g => g.Count() > 1);
        if (aDuplicateInBatch is not null)
        {
            throw new ConflictException($"SKU '{aDuplicateInBatch.Key}' is repeated in the request.");
        }

        // Every referenced category must exist (check each distinct id once).
        foreach (var aCategoryId in theRequests.Select(r => r.CategoryId).Distinct())
        {
            await EnsureCategoryExistsAsync(aCategoryId, theCancellationToken);
        }

        // None of the SKUs may already exist in the store.
        foreach (var aSku in aSkuByIndex)
        {
            if (await myProductRepository.ExistsBySkuAsync(aSku.Value, theCancellationToken))
            {
                throw new ConflictException($"A product with SKU '{aSku.Value}' already exists.");
            }
        }

        var aProducts = theRequests
            .Select((aRequest, aIndex) => Build(aRequest, aSkuByIndex[aIndex]))
            .ToList();

        await myProductRepository.AddRangeAsync(aProducts, theCancellationToken);
        return aProducts;
    }

    private async Task EnsureCategoryExistsAsync(Guid theCategoryId, CancellationToken theCancellationToken)
    {
        var aCategory = await myCategoryRepository.GetByIdAsync(theCategoryId, theCancellationToken);
        if (aCategory is null)
        {
            throw new NotFoundException($"Category '{theCategoryId}' not found.");
        }
    }

    private static Product Build(NewProduct theRequest, Sku theSku) =>
        Product.Create(
            theRequest.Name,
            theSku,
            theRequest.Color,
            (Size)theRequest.Size,
            Money.Create(theRequest.Price, theRequest.Currency),
            theRequest.StockQuantity,
            theRequest.CategoryId);
}
