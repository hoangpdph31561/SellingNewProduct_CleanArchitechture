namespace SellingNewProduct.Infrastructure.MongoDB.Saga.Models;

/// <summary>
/// Durable record of what a saga changed in MongoDB — the per-product stock deltas it removed.
/// Written INSIDE the Mongo transaction (same SaveChanges as the stock change), so it exists iff the
/// Mongo side committed. Used to undo the change idempotently, both by the in-process compensation
/// and by the crash-recovery worker. <see cref="Reverted"/> guards against double-undo.
/// </summary>
internal sealed class SagaEffectDocument
{
    public Guid Id { get; set; }            // = SagaId
    public bool Reverted { get; set; }
    public List<StockDeltaDocument> Deltas { get; set; } = new();
}

/// <summary>One product's stock delta: how much this saga removed from stock (negative if it added).</summary>
internal sealed class StockDeltaDocument
{
    public Guid ProductId { get; set; }
    public int Removed { get; set; }
}
