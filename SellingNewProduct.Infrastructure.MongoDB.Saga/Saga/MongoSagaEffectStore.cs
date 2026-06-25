using Microsoft.EntityFrameworkCore;
using SellingNewProduct.Infrastructure.MongoDB.Saga.Models;
using SellingNewProduct.Infrastructure.MongoDB.Saga.Persistence;
using SellingNewProduct.Infrastructure.Saga.Core.Recovery;

namespace SellingNewProduct.Infrastructure.MongoDB.Saga.Saga;

/// <summary>
/// Stores and reverts the durable saga effect (stock deltas) in MongoDB. <see cref="RecordAsync"/>
/// is called by the saga-aware write repository and only ENQUEUES the effect on the shared context,
/// so the repository's single SaveChanges persists the stock change and this record together inside
/// the Mongo transaction (atomic). <see cref="RevertAsync"/> undoes the deltas in its own Mongo
/// transaction and is idempotent — safe to call from both the in-process compensation and recovery.
/// </summary>
internal sealed class MongoSagaEffectStore : ISagaEffectStore
{
    private readonly MongoAppDbContext myMongoAppDbContext;

    public MongoSagaEffectStore(MongoAppDbContext theMongoAppDbContext)
    {
        myMongoAppDbContext = theMongoAppDbContext;
    }

    /// <summary>Enqueues the effect on the current context (persisted by the caller's SaveChanges, in the saga's Mongo transaction).</summary>
    public void Record(Guid theSagaId, IReadOnlyDictionary<Guid, int> theStockRemoved)
    {
        myMongoAppDbContext.SagaEffects.Add(new SagaEffectDocument
        {
            Id = theSagaId,
            Reverted = false,
            Deltas = theStockRemoved.Select(kv => new StockDeltaDocument { ProductId = kv.Key, Removed = kv.Value }).ToList()
        });
    }

    public async Task<IReadOnlyList<Guid>> GetPendingSagaIdsAsync(CancellationToken theCancellationToken = default)
    {
        return await myMongoAppDbContext.SagaEffects.AsNoTracking()
            .Where(e => !e.Reverted)
            .Select(e => e.Id)
            .ToListAsync(theCancellationToken);
    }

    public async Task RevertAsync(Guid theSagaId, CancellationToken theCancellationToken = default)
    {
        await using var aTransaction = await myMongoAppDbContext.Database.BeginTransactionAsync(theCancellationToken);

        var aEffect = await myMongoAppDbContext.SagaEffects
            .FirstOrDefaultAsync(e => e.Id == theSagaId, theCancellationToken);

        if (aEffect is null || aEffect.Reverted)
        {
            return; // Nothing to do / already reverted — idempotent.
        }

        var aProductIds = aEffect.Deltas.Select(d => d.ProductId).ToList();
        var aProducts = (await myMongoAppDbContext.Products
            .Where(p => aProductIds.Contains(p.Id))
            .ToListAsync(theCancellationToken))
            .ToDictionary(p => p.Id);

        foreach (var aDelta in aEffect.Deltas)
        {
            if (aProducts.TryGetValue(aDelta.ProductId, out var aProduct))
            {
                // Add back exactly what the saga removed (negative delta means it had been added).
                aProduct.StockQuantity += aDelta.Removed;
                aProduct.UpdatedAtUtc = DateTime.UtcNow;
            }
        }

        aEffect.Reverted = true;
        await myMongoAppDbContext.SaveChangesAsync(theCancellationToken);
        await aTransaction.CommitAsync(theCancellationToken);
    }

    public async Task RemoveAsync(Guid theSagaId, CancellationToken theCancellationToken = default)
    {
        var aEffect = await myMongoAppDbContext.SagaEffects
            .FirstOrDefaultAsync(e => e.Id == theSagaId, theCancellationToken);

        if (aEffect is null)
        {
            return;
        }

        myMongoAppDbContext.SagaEffects.Remove(aEffect);
        await myMongoAppDbContext.SaveChangesAsync(theCancellationToken);
    }
}
