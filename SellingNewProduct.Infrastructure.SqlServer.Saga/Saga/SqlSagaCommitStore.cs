using Microsoft.EntityFrameworkCore;
using SellingNewProduct.Infrastructure.Saga.Core.Recovery;
using SellingNewProduct.Infrastructure.SqlServer.Saga.Persistence;

namespace SellingNewProduct.Infrastructure.SqlServer.Saga.Saga;

/// <summary>Reads/removes the SQL pivot-commit markers for the recovery worker.</summary>
internal sealed class SqlSagaCommitStore : ISagaCommitStore
{
    private readonly AppDbContext myAppDbContext;

    public SqlSagaCommitStore(AppDbContext theAppDbContext)
    {
        myAppDbContext = theAppDbContext;
    }

    public Task<bool> ExistsAsync(Guid theSagaId, CancellationToken theCancellationToken = default)
        => myAppDbContext.SagaCommits.AsNoTracking().AnyAsync(r => r.SagaId == theSagaId, theCancellationToken);

    public async Task RemoveAsync(Guid theSagaId, CancellationToken theCancellationToken = default)
    {
        var aRecord = await myAppDbContext.SagaCommits.FirstOrDefaultAsync(r => r.SagaId == theSagaId, theCancellationToken);
        if (aRecord is null)
        {
            return;
        }

        myAppDbContext.SagaCommits.Remove(aRecord);
        await myAppDbContext.SaveChangesAsync(theCancellationToken);
    }
}
