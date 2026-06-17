using SellingNewProduct.Domain.Abstractions;
using SellingNewProduct.Domain.Commands;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Products;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;
using SellingNewProduct.Domain.Repositories;
using SellingNewProduct.Domain.ValueObjects;

namespace SellingNewProduct.Domain.Services;

internal sealed class ProductService : IProductService
{
    private readonly IProductRepository myProductRepository;
    private readonly ICategoryRepository myCategoryRepository;

    public ProductService(IProductRepository theProductRepository, ICategoryRepository theCategoryRepository)
    {
        myProductRepository = theProductRepository;
        myCategoryRepository = theCategoryRepository;
    }

    public Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken theCancellationToken = default)
        => myProductRepository.GetAllAsync(theCancellationToken);

    public Task<Product?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default)
        => myProductRepository.GetByIdAsync(theId, theCancellationToken);

    public async Task<Product> CreateAsync(CreateProductCommand theCommand, CancellationToken theCancellationToken = default)
    {
        // A product must reference an existing category.
        await EnsureCategoryExistsAsync(theCommand.CategoryId, theCancellationToken);

        // Normalize the SKU through its value object, then enforce the "unique SKU" business rule.
        var aSku = Sku.Create(theCommand.Sku);
        if (await myProductRepository.ExistsBySkuAsync(aSku.Value, theCancellationToken))
        {
            throw new ConflictException($"A product with SKU '{aSku.Value}' already exists.");
        }

        var aProduct = BuildProduct(theCommand, aSku);

        await myProductRepository.AddAsync(aProduct, theCancellationToken);
        return aProduct;
    }

    public async Task<IReadOnlyList<Product>> CreateManyAsync(IReadOnlyList<CreateProductCommand> theCommands, CancellationToken theCancellationToken = default)
    {
        if (theCommands is null || theCommands.Count == 0)
        {
            throw new DomainException("At least one product is required.");
        }

        // Normalize all SKUs first; reject duplicates WITHIN the batch before touching the database.
        var aSkuByIndex = theCommands.Select(c => Sku.Create(c.Sku)).ToList();
        var aDuplicateInBatch = aSkuByIndex.GroupBy(s => s.Value).FirstOrDefault(g => g.Count() > 1);
        if (aDuplicateInBatch is not null)
        {
            throw new ConflictException($"SKU '{aDuplicateInBatch.Key}' is repeated in the request.");
        }

        // Every referenced category must exist (check each distinct id once).
        foreach (var aCategoryId in theCommands.Select(c => c.CategoryId).Distinct())
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

        var aProducts = theCommands
            .Select((aCommand, aIndex) => BuildProduct(aCommand, aSkuByIndex[aIndex]))
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

    private static Product BuildProduct(CreateProductCommand theCommand, Sku theSku) =>
        Product.Create(
            theCommand.Name,
            theSku,
            theCommand.Color,
            (Size)theCommand.Size,
            Money.Create(theCommand.Price, theCommand.Currency),
            theCommand.StockQuantity,
            theCommand.CategoryId);

    public Task<ProductSummaryView?> GetSummaryByIdAsync(Guid theProductId, CancellationToken theCancellationToken = default)
        => myProductRepository.GetSummaryByIdAsync(theProductId, theCancellationToken);

    public Task<PagedResult<ProductSummaryView>> SearchAsync(ProductSearchQuery theQuery, CancellationToken theCancellationToken = default)
        => myProductRepository.SearchAsync(theQuery, theCancellationToken);
}
