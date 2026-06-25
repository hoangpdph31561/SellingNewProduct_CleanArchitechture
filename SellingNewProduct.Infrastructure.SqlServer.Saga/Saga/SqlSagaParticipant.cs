using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SellingNewProduct.Infrastructure.Saga.Core.Saga;
using SellingNewProduct.Infrastructure.SqlServer.Saga.Models;
using SellingNewProduct.Infrastructure.SqlServer.Saga.Persistence;

namespace SellingNewProduct.Infrastructure.SqlServer.Saga.Saga;

/// <summary>
/// The SQL Server enrollment in a saga — the saga's PIVOT. It opens a real EF Core transaction on
/// the shared <see cref="AppDbContext"/>; every Order/Payment write in the same scope enlists in
/// it and stays pending until <see cref="CommitAsync"/>. Because this is a genuine transaction,
/// <see cref="RollbackAsync"/> discards the SQL work natively — no compensation needed on this side.
/// </summary>
internal sealed class SqlSagaParticipant : ISagaParticipant, IAsyncDisposable
{
    private readonly AppDbContext myAppDbContext;
    private readonly SagaContext mySagaContext;
    private IDbContextTransaction? myTransaction;

    public SqlSagaParticipant(AppDbContext theAppDbContext, SagaContext theSagaContext)
    {
        myAppDbContext = theAppDbContext;
        mySagaContext = theSagaContext;
    }

    /// <summary>SQL Server is the saga pivot: it commits last.</summary>
    public bool IsPivot => true;

    public async Task BeginAsync(CancellationToken theCancellationToken = default)
    {
        myTransaction ??= await myAppDbContext.Database.BeginTransactionAsync(theCancellationToken);
    }

    public async Task CommitAsync(CancellationToken theCancellationToken = default)
    {
        if (myTransaction is null)
        {
            return;
        }

        // Write the pivot-commit marker INSIDE the business transaction, so it becomes durable
        // atomically with the order/payment writes. Recovery reads it to confirm the pivot committed.
        myAppDbContext.SagaCommits.Add(new SagaCommitRecord
        {
            SagaId = mySagaContext.SagaId,
            CommittedUtc = DateTime.UtcNow
        });
        await myAppDbContext.SaveChangesAsync(theCancellationToken);

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
