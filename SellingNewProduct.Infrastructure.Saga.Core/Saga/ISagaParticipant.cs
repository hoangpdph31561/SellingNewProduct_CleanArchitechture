namespace SellingNewProduct.Infrastructure.Saga.Core.Saga;

/// <summary>
/// One database's enrollment in a saga. Each backing store contributes a participant so the
/// unit of work can drive them uniformly without referencing any concrete database type.
///
/// Both stores here wrap a real local transaction (EF Core <c>BeginTransaction</c>): writes stay
/// pending until <see cref="CommitAsync"/> and <see cref="RollbackAsync"/> discards them natively.
/// The SQL participant is the pivot (commits last). The Mongo participant commits first; once it has
/// committed, the ONLY way to undo it is the compensation recorded on <see cref="SagaContext"/> —
/// which runs if the pivot's commit then fails. MongoDB local transactions require a replica set.
/// </summary>
public interface ISagaParticipant
{
    /// <summary>
    /// True for the saga's PIVOT — the store committed LAST. Non-pivot stores commit first; if the
    /// pivot's commit then fails, only those earlier (already-committed) stores need compensating.
    /// Exactly one participant should be the pivot (the SQL side here).
    /// </summary>
    bool IsPivot { get; }

    /// <summary>Enlists this store in the saga (opens its local transaction).</summary>
    Task BeginAsync(CancellationToken theCancellationToken = default);

    /// <summary>Commits this store's pending local work.</summary>
    Task CommitAsync(CancellationToken theCancellationToken = default);

    /// <summary>Discards this store's pending local work (native rollback where supported).</summary>
    Task RollbackAsync(CancellationToken theCancellationToken = default);
}
