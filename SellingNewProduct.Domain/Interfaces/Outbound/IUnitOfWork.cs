namespace SellingNewProduct.Domain.Interfaces.Outbound;

/// <summary>
/// A unit of work: the seam that lets a write-side handler group several repository
/// writes into ONE atomic transaction (all-or-nothing). The implementation lives in each
/// infrastructure project and wraps that database's native transaction.
///
/// The contract exposes the full transaction verb set:
/// <list type="bullet">
/// <item><b>Begin</b> — <see cref="BeginTransactionAsync"/> opens the transaction.</item>
/// <item><b>Commit</b> — <see cref="IUnitOfWorkTransaction.CommitAsync"/> makes the writes durable.</item>
/// <item><b>Rollback</b> — <see cref="IUnitOfWorkTransaction.RollbackAsync"/> discards/undoes them.</item>
/// <item><b>End</b> — <see cref="IAsyncDisposable.DisposeAsync"/> (via <c>await using</c>) ends the
/// transaction scope, rolling back if it was neither committed nor rolled back.</item>
/// </list>
///
/// MongoDB note: the Mongo implementation maps to a multi-document transaction, which the
/// server only supports when running as a replica set (or sharded cluster). On a standalone
/// <c>mongod</c> the begin/commit will fail — that is by design and is the lesson behind
/// requiring a replica set for the order-confirm flow.
///
/// Saga (hybrid) note: in the polyglot provider a single transaction cannot span SQL Server and
/// MongoDB (there is no shared 2-phase commit). There the SAME port is implemented as a saga —
/// Begin starts it, each database commits locally, Commit finalizes, and Rollback replays
/// compensations to undo the local commits. The domain code is identical either way.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>Begin: opens a transaction shared by every repository resolved in the same scope.</summary>
    Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken theCancellationToken = default);
}

/// <summary>A live transaction handle. Commit on success, Rollback to undo, Dispose ("End") to finish.</summary>
public interface IUnitOfWorkTransaction : IAsyncDisposable
{
    /// <summary>Commit: make every write in the transaction durable.</summary>
    Task CommitAsync(CancellationToken theCancellationToken = default);

    /// <summary>Rollback: discard/undo every write in the transaction.</summary>
    Task RollbackAsync(CancellationToken theCancellationToken = default);
}
