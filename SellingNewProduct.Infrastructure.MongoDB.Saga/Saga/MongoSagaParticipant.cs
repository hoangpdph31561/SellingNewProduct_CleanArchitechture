using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SellingNewProduct.Infrastructure.MongoDB.Saga.Persistence;
using SellingNewProduct.Infrastructure.Saga.Core.Saga;

namespace SellingNewProduct.Infrastructure.MongoDB.Saga.Saga;

/// <summary>
/// MongoDB's enrollment in a saga, wrapping a REAL MongoDB multi-document transaction on the shared
/// <see cref="MongoAppDbContext"/>. Every catalogue/people write in the same scope stays pending in
/// this transaction until <see cref="CommitAsync"/>, so the batch is atomic (no partial writes) and
/// <see cref="RollbackAsync"/> discards it natively.
///
/// This is the NON-pivot store: it commits BEFORE the SQL pivot. If the SQL commit then fails, this
/// transaction is already committed and can only be undone by the snapshot compensation recorded on
/// <see cref="SagaContext"/>. MongoDB transactions require the server to run as a replica set.
/// </summary>
internal sealed class MongoSagaParticipant : ISagaParticipant, IAsyncDisposable
{
    private readonly MongoAppDbContext myMongoAppDbContext;
    private IDbContextTransaction? myTransaction;

    public MongoSagaParticipant(MongoAppDbContext theMongoAppDbContext)
    {
        myMongoAppDbContext = theMongoAppDbContext;
    }

    /// <summary>MongoDB is not the pivot: it commits before the SQL side.</summary>
    public bool IsPivot => false;

    public async Task BeginAsync(CancellationToken theCancellationToken = default)
    {
        myTransaction ??= await myMongoAppDbContext.Database.BeginTransactionAsync(theCancellationToken);
    }

    public async Task CommitAsync(CancellationToken theCancellationToken = default)
    {
        if (myTransaction is null)
        {
            return;
        }

        await myTransaction.CommitAsync(theCancellationToken);
        await myTransaction.DisposeAsync();
        myTransaction = null;
    }

    public async Task RollbackAsync(CancellationToken theCancellationToken = default)
    {
        if (myTransaction is null)
        {
            return;
        }

        await myTransaction.RollbackAsync(theCancellationToken);
        await myTransaction.DisposeAsync();
        myTransaction = null;
    }

    public async ValueTask DisposeAsync()
    {
        if (myTransaction is not null)
        {
            await myTransaction.DisposeAsync();
            myTransaction = null;
        }
    }
}
