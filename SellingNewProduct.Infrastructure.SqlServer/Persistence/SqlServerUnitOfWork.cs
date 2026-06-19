using Microsoft.EntityFrameworkCore.Storage;
using SellingNewProduct.Domain.Interfaces.Outbound;

namespace SellingNewProduct.Infrastructure.SqlServer.Persistence;

/// <summary>
/// SQL Server unit of work. Opens an EF Core transaction on the shared <see cref="AppDbContext"/>;
/// every repository resolved in the same scope uses that same context, so their SaveChanges
/// calls all enlist in this transaction and commit together.
/// </summary>
internal sealed class SqlServerUnitOfWork : IUnitOfWork
{
    private readonly AppDbContext myAppDbContext;

    public SqlServerUnitOfWork(AppDbContext theAppDbContext)
    {
        myAppDbContext = theAppDbContext;
    }

    public async Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken theCancellationToken = default)
    {
        var aTransaction = await myAppDbContext.Database.BeginTransactionAsync(theCancellationToken);
        return new EfCoreUnitOfWorkTransaction(aTransaction);
    }
}

/// <summary>Adapts EF Core's <see cref="IDbContextTransaction"/> to the domain transaction contract.</summary>
internal sealed class EfCoreUnitOfWorkTransaction : IUnitOfWorkTransaction
{
    private readonly IDbContextTransaction myTransaction;

    public EfCoreUnitOfWorkTransaction(IDbContextTransaction theTransaction)
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
