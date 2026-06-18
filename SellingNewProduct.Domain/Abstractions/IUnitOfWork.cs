namespace SellingNewProduct.Domain.Abstractions;

/// <summary>
/// A unit of work: the seam that lets a write-side handler group several repository
/// writes into ONE atomic transaction (all-or-nothing). The implementation lives in each
/// infrastructure project and wraps that database's native transaction.
///
/// MongoDB note: the Mongo implementation maps to a multi-document transaction, which the
/// server only supports when running as a replica set (or sharded cluster). On a standalone
/// <c>mongod</c> the begin/commit will fail — that is by design and is the lesson behind
/// requiring a replica set for the order-confirm flow.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>Opens a transaction shared by every repository resolved in the same scope.</summary>
    Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken theCancellationToken = default);
}

/// <summary>A live transaction handle. Commit on success, otherwise dispose to roll back.</summary>
public interface IUnitOfWorkTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken theCancellationToken = default);

    Task RollbackAsync(CancellationToken theCancellationToken = default);
}
