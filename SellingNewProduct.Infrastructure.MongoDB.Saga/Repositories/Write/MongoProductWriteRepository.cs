using System.Text.Json;
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
/// MongoDB write side for the catalogue, made SAGA-AWARE following the commit-per-step model. When a
/// saga is active, a stock change is COMMITTED to MongoDB immediately — there is no transaction held
/// open until the end of the request. In that same Mongo transaction the repository records its step
/// in the single saga ledger (the <c>RevertStock</c> compensation plus the per-product deltas), so
/// the change and its undo information become durable together (atomic).
///
/// MongoDB is a <see cref="SagaStepKind.Compensatable"/> step that runs BEFORE the SQL pivot: if a
/// later step fails, the orchestrator replays <see cref="MongoStockCompensationHandler"/> to add the
/// stock back; the very same handler is used by crash recovery.
/// </summary>
internal sealed class MongoProductWriteRepository : IProductWriteRepository
{
    private const int DeletedStatus = (int)EntityStatus.Deleted;
    private const string StockStepName = "DecreaseStock";

    private readonly MongoAppDbContext myMongoAppDbContext;
    private readonly SagaContext mySagaContext;
    private readonly MongoSagaStore mySagaStore;

    public MongoProductWriteRepository(
        MongoAppDbContext theMongoAppDbContext,
        SagaContext theSagaContext,
        MongoSagaStore theSagaStore)
    {
        myMongoAppDbContext = theMongoAppDbContext;
        mySagaContext = theSagaContext;
        mySagaStore = theSagaStore;
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
        var aDeltas = new List<StockDelta>();
        foreach (var aProduct in aProducts)
        {
            if (aDocuments.TryGetValue(aProduct.Id, out var aDocument))
            {
                var aRemoved = aDocument.StockQuantity - aProduct.StockQuantity;
                if (aRemoved != 0)
                {
                    aDeltas.Add(new StockDelta(aProduct.Id, aRemoved));
                }

                ProductMapper.MapInto(aDocument, aProduct);
            }
        }

        if (!mySagaContext.IsActive || aDeltas.Count == 0)
        {
            // Outside a saga (or no stock change): a plain immediate commit, nothing to enroll.
            await myMongoAppDbContext.SaveChangesAsync(theCancellationToken);
            return;
        }

        // Saga step: commit the stock change and record the undo info atomically, then COMMIT NOW.
        var aStep = new SagaStepInfo(
            StockStepName,
            SagaStepKind.Compensatable,
            MongoStockCompensationHandler.Type,
            JsonSerializer.Serialize(aDeltas),
            Compensated: false);

        await using var aTransaction = await myMongoAppDbContext.Database.BeginTransactionAsync(theCancellationToken);
        await mySagaStore.StageStepAsync(mySagaContext.SagaId, aStep, theCancellationToken);
        await myMongoAppDbContext.SaveChangesAsync(theCancellationToken);
        await aTransaction.CommitAsync(theCancellationToken);
    }
}
