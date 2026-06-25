using Microsoft.EntityFrameworkCore;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Products;
using SellingNewProduct.Domain.Interfaces.Outbound;
using SellingNewProduct.Infrastructure.MongoDB.Saga.Mapping;
using SellingNewProduct.Infrastructure.MongoDB.Saga.Persistence;
using SellingNewProduct.Infrastructure.MongoDB.Saga.Saga;
using SellingNewProduct.Infrastructure.Saga.Core.Saga;

namespace SellingNewProduct.Infrastructure.MongoDB.Saga.Repositories.Write;

/// <summary>
/// MongoDB write side for the catalogue, made SAGA-AWARE. When a saga is active these writes run
/// inside the real MongoDB transaction opened by <c>MongoSagaParticipant</c>, so the batch is atomic
/// and stays pending until the saga commits. MongoDB is the NON-pivot store (commits before SQL):
/// once committed, the only way to undo it is a compensation.
///
/// So on a stock change this repository records the per-product delta into a durable
/// <c>SagaEffect</c> — written in the SAME SaveChanges (same Mongo transaction) — and registers an
/// in-process compensation that reverts it. The very same effect lets the crash-recovery worker undo
/// the change if the process dies between the Mongo and SQL commits. Both undo paths call the same
/// idempotent <see cref="MongoSagaEffectStore.RevertAsync"/>.
/// </summary>
internal sealed class MongoProductWriteRepository : IProductWriteRepository
{
    private const int DeletedStatus = (int)EntityStatus.Deleted;

    private readonly MongoAppDbContext myMongoAppDbContext;
    private readonly SagaContext mySagaContext;
    private readonly MongoSagaEffectStore myEffectStore;

    public MongoProductWriteRepository(
        MongoAppDbContext theMongoAppDbContext,
        SagaContext theSagaContext,
        MongoSagaEffectStore theEffectStore)
    {
        myMongoAppDbContext = theMongoAppDbContext;
        mySagaContext = theSagaContext;
        myEffectStore = theEffectStore;
    }

    public async Task<IReadOnlyList<Product>> GetByIdsAsync(IReadOnlyCollection<Guid> theIds, CancellationToken theCancellationToken = default)
    {
        if (theIds.Count == 0)
        {
            return [];
        }

        var aDocuments = await myMongoAppDbContext.Products
            .AsNoTracking()
            .Where(r => theIds.Contains(r.Id) && r.Status != DeletedStatus)
            .ToListAsync(theCancellationToken);

        return aDocuments.Select(ProductMapper.ToDomain).ToList();
    }

    public async Task<bool> ExistsBySkuAsync(string theSku, CancellationToken theCancellationToken = default)
    {
        return await myMongoAppDbContext.Products
            .AsNoTracking()
            .AnyAsync(r => r.Sku == theSku && r.Status != DeletedStatus, theCancellationToken);
    }

    public async Task AddAsync(Product theProduct, CancellationToken theCancellationToken = default)
    {
        myMongoAppDbContext.Products.Add(ProductMapper.ToDocument(theProduct));
        await myMongoAppDbContext.SaveChangesAsync(theCancellationToken);
    }

    public async Task AddRangeAsync(IEnumerable<Product> theProducts, CancellationToken theCancellationToken = default)
    {
        myMongoAppDbContext.Products.AddRange(theProducts.Select(ProductMapper.ToDocument));
        await myMongoAppDbContext.SaveChangesAsync(theCancellationToken);
    }

    public Task UpdateAsync(Product theProduct, CancellationToken theCancellationToken = default)
        => UpdateRangeAsync(new[] { theProduct }, theCancellationToken);

    public async Task UpdateRangeAsync(IEnumerable<Product> theProducts, CancellationToken theCancellationToken = default)
    {
        var aProducts = theProducts.ToList();
        var aIds = aProducts.Select(p => p.Id).ToList();

        var aDocuments = (await myMongoAppDbContext.Products
            .Where(r => aIds.Contains(r.Id))
            .ToListAsync(theCancellationToken))
            .ToDictionary(r => r.Id);

        // Capture the stock delta (old - new) BEFORE mutating, so the saga can be undone.
        var aStockRemoved = new Dictionary<Guid, int>();
        foreach (var aProduct in aProducts)
        {
            if (aDocuments.TryGetValue(aProduct.Id, out var aDocument))
            {
                aStockRemoved[aProduct.Id] = aDocument.StockQuantity - aProduct.StockQuantity;
                ProductMapper.MapInto(aDocument, aProduct);
            }
        }

        if (mySagaContext.IsActive && aStockRemoved.Count > 0)
        {
            // Record the effect in the SAME SaveChanges (Mongo transaction) and register the undo.
            myEffectStore.Record(mySagaContext.SagaId, aStockRemoved);

            var aSagaId = mySagaContext.SagaId;
            mySagaContext.RegisterCompensation($"RevertStock:{aSagaId}",
                ct => myEffectStore.RevertAsync(aSagaId, ct));
        }

        await myMongoAppDbContext.SaveChangesAsync(theCancellationToken);
    }
}
