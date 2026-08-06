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
    // Build the domain objects across cores once an import is large enough to be worth the overhead.
    private const int ParallelBuildThreshold = 100;

    // Insert in chunks so a huge import is not one giant command / transaction.
    private const int BulkWriteBatchSize = 500;

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

        // Every referenced category must exist (check each distinct id once). These calls share one
        // DbContext, which is NOT thread-safe, so they run sequentially — do NOT wrap them in
        // Task.WhenAll.
        foreach (var aCategoryId in theRequests.Select(r => r.CategoryId).Distinct())
        {
            await EnsureCategoryExistsAsync(aCategoryId, theCancellationToken);
        }

        // None of the SKUs may already exist. Instead of one query per SKU (an N+1 that also cannot
        // be parallelized on a shared DbContext), ask the store for all taken SKUs in a SINGLE
        // round-trip and diff in memory: batching beats fan-out here.
        var aSkuValues = aSkuByIndex.Select(s => s.Value).ToList();
        var aExistingSkus = await myProductRepository.GetExistingSkusAsync(aSkuValues, theCancellationToken);
        if (aExistingSkus.Count > 0)
        {
            throw new ConflictException($"A product with SKU '{aExistingSkus.First()}' already exists.");
        }

        var aProducts = await BuildManyAsync(theRequests, aSkuByIndex, theCancellationToken);

        // Bulk write: insert in chunks. Each chunk is one AddRange + SaveChanges round-trip, so a
        // very large import does not build a single oversized command / transaction.
        foreach (var aChunk in aProducts.Chunk(BulkWriteBatchSize))
        {
            await myProductRepository.AddRangeAsync(aChunk, theCancellationToken);
        }

        return aProducts;
    }

    /// <summary>
    /// Maps every request to a domain <see cref="Product"/>. Mapping is pure CPU work that touches no
    /// shared state, so a large batch is spread across cores with a bounded degree of parallelism
    /// (Task.Run offloads to the thread pool; the SemaphoreSlim inside <see cref="AsyncParallel"/>
    /// caps how many run at once). A small batch maps inline — the parallel overhead is not worth it.
    /// Results keep the input order either way.
    /// </summary>
    private static async Task<List<Product>> BuildManyAsync(
        IReadOnlyList<NewProduct> theRequests,
        IReadOnlyList<Sku> theSkuByIndex,
        CancellationToken theCancellationToken)
    {
        if (theRequests.Count < ParallelBuildThreshold)
        {
            return theRequests.Select((aRequest, aIndex) => Build(aRequest, theSkuByIndex[aIndex])).ToList();
        }

        var aBuilt = await AsyncParallel.ForEachAsync(
            Enumerable.Range(0, theRequests.Count),
            Environment.ProcessorCount,
            (aIndex, aCancellationToken) => Task.Run(() => Build(theRequests[aIndex], theSkuByIndex[aIndex]), aCancellationToken),
            theCancellationToken);

        return aBuilt.ToList();
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
