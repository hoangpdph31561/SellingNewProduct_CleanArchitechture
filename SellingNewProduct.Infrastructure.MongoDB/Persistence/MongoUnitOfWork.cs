using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SellingNewProduct.Domain.Abstractions;

namespace SellingNewProduct.Infrastructure.MongoDB.Persistence;

/// <summary>
/// MongoDB unit of work. Opens a transaction on the shared <see cref="MongoAppDbContext"/> via
/// the MongoDB EF Core provider. Every repository resolved in the same scope shares that context,
/// so their SaveChanges calls enlist in this transaction and commit together.
///
/// IMPORTANT: MongoDB multi-document transactions require the server to run as a REPLICA SET
/// (or sharded cluster). Against a standalone <c>mongod</c> this call throws — which is exactly
/// why the order-confirm flow needs a replica set. See docs/mongo-replica-set.md.
/// </summary>
internal sealed class MongoUnitOfWork : IUnitOfWork
{
    private readonly MongoAppDbContext myMongoAppDbContext;

    public MongoUnitOfWork(MongoAppDbContext theMongoAppDbContext)
    {
        myMongoAppDbContext = theMongoAppDbContext;
    }

    public async Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken theCancellationToken = default)
    {
        var aTransaction = await myMongoAppDbContext.Database.BeginTransactionAsync(theCancellationToken);
        return new MongoUnitOfWorkTransaction(aTransaction);
    }
}

/// <summary>Adapts EF Core's <see cref="IDbContextTransaction"/> to the domain transaction contract.</summary>
internal sealed class MongoUnitOfWorkTransaction : IUnitOfWorkTransaction
{
    private readonly IDbContextTransaction myTransaction;

    public MongoUnitOfWorkTransaction(IDbContextTransaction theTransaction)
    {
        myTransaction = theTransaction;
    }

    public Task CommitAsync(CancellationToken theCancellationToken = default)
        => myTransaction.CommitAsync(theCancellationToken);

    public Task RollbackAsync(CancellationToken theCancellationToken = default)
        => myTransaction.RollbackAsync(theCancellationToken);

    public ValueTask DisposeAsync()
        => myTransaction.DisposeAsync();
}
