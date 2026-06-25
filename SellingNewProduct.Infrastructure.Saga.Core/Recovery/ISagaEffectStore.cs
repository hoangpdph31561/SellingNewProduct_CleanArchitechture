namespace SellingNewProduct.Infrastructure.Saga.Core.Recovery;

/// <summary>
/// Durable record of the effect a saga applied to MongoDB (the stock deltas), written INSIDE the
/// Mongo transaction so it exists iff the Mongo side committed. It powers BOTH undo paths:
/// the in-process compensation (when the SQL pivot fails) and the crash-recovery worker — both call
/// <see cref="RevertAsync"/>, which is idempotent (a per-effect "reverted" flag flipped in a Mongo
/// transaction), so applying it twice is safe.
/// </summary>
public interface ISagaEffectStore
{
    /// <summary>Sagas whose Mongo effect committed but has not yet been reverted — recovery candidates.</summary>
    Task<IReadOnlyList<Guid>> GetPendingSagaIdsAsync(CancellationToken theCancellationToken = default);

    /// <summary>Undoes the recorded stock deltas for a saga and marks the effect reverted. Idempotent.</summary>
    Task RevertAsync(Guid theSagaId, CancellationToken theCancellationToken = default);

    /// <summary>Deletes the effect once a saga is confirmed consistent (its pivot committed).</summary>
    Task RemoveAsync(Guid theSagaId, CancellationToken theCancellationToken = default);
}
