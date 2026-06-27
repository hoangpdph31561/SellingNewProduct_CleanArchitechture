using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SellingNewProduct.Infrastructure.MongoDB.Saga.Persistence;
using SellingNewProduct.Infrastructure.Saga.Core.Saga;

namespace SellingNewProduct.Infrastructure.MongoDB.Saga.Saga;

/// <summary>
/// Undoes a Mongo stock step: adds back exactly the quantities the saga removed. This is the
/// compensation that runs when a LATER step (the SQL pivot) fails, or during crash recovery.
///
/// It is idempotent and crash-safe because the stock change AND the "step compensated" flag are
/// written in ONE Mongo transaction: if the step is already marked compensated it does nothing, so
/// running it twice can never double-revert stock.
/// </summary>
internal sealed class MongoStockCompensationHandler : ISagaCompensationHandler
{
    /// <summary>Must match the type recorded by the stock step in <c>MongoProductWriteRepository</c>.</summary>
    public const string Type = "RevertStock";

    private readonly MongoAppDbContext myContext;

    public MongoStockCompensationHandler(MongoAppDbContext theContext)
    {
        myContext = theContext;
    }

    public string CompensationType => Type;

    public async Task CompensateAsync(SagaCompensationContext theContext, CancellationToken theCancellationToken = default)
    {
        await using var aTransaction = await myContext.Database.BeginTransactionAsync(theCancellationToken);

        var aSaga = await myContext.SagaInstances
            .FirstOrDefaultAsync(s => s.Id == theContext.SagaId, theCancellationToken);

        var aStep = aSaga?.Steps.FirstOrDefault(s => s.Name == theContext.StepName);
        if (aStep is null || aStep.Compensated)
        {
            return; // Already undone (or gone) — idempotent no-op.
        }

        var aDeltas = JsonSerializer.Deserialize<List<StockDelta>>(theContext.CompensationData) ?? new List<StockDelta>();
        var aProductIds = aDeltas.Select(d => d.ProductId).ToList();

        var aProducts = (await myContext.Products
            .Where(p => aProductIds.Contains(p.Id))
            .ToListAsync(theCancellationToken))
            .ToDictionary(p => p.Id);

        foreach (var aDelta in aDeltas)
        {
            if (aProducts.TryGetValue(aDelta.ProductId, out var aProduct))
            {
                // Add back exactly what the saga removed (a negative delta means it had been added).
                aProduct.StockQuantity += aDelta.Removed;
                aProduct.UpdatedAtUtc = DateTime.UtcNow;
            }
        }

        aStep.Compensated = true;
        aSaga!.UpdatedUtc = DateTime.UtcNow;

        await myContext.SaveChangesAsync(theCancellationToken);
        await aTransaction.CommitAsync(theCancellationToken);
    }
}
