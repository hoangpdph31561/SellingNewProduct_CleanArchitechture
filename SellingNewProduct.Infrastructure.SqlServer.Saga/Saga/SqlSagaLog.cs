using Microsoft.EntityFrameworkCore;
using SellingNewProduct.Infrastructure.Saga.Core.Saga;
using SellingNewProduct.Infrastructure.SqlServer.Saga.Models;
using SellingNewProduct.Infrastructure.SqlServer.Saga.Persistence;

namespace SellingNewProduct.Infrastructure.SqlServer.Saga.Saga;

/// <summary>
/// Durable saga ledger backed by the dedicated <see cref="SagaLogDbContext"/>. Writes here are
/// intentionally on a different context/connection than the business <see cref="AppDbContext"/>,
/// so the final outcome row is kept even when the saga compensates and the business transaction
/// rolls back.
/// </summary>
internal sealed class SqlSagaLog : ISagaLog
{
    private readonly SagaLogDbContext mySagaLogDbContext;

    public SqlSagaLog(SagaLogDbContext theSagaLogDbContext)
    {
        mySagaLogDbContext = theSagaLogDbContext;
    }

    public async Task StartAsync(Guid theSagaId, CancellationToken theCancellationToken = default)
    {
        mySagaLogDbContext.SagaTransactions.Add(new SagaTransactionRecord
        {
            Id = theSagaId,
            Status = (int)SagaStatus.Started,
            StartedUtc = DateTime.UtcNow,
            Steps = string.Empty
        });

        await mySagaLogDbContext.SaveChangesAsync(theCancellationToken);
    }

    public async Task CompleteAsync(
        Guid theSagaId,
        SagaStatus theFinalStatus,
        IReadOnlyList<string> theStepNames,
        CancellationToken theCancellationToken = default)
    {
        var aRecord = await mySagaLogDbContext.SagaTransactions
            .FirstOrDefaultAsync(r => r.Id == theSagaId, theCancellationToken);

        if (aRecord is null)
        {
            return;
        }

        aRecord.Status = (int)theFinalStatus;
        aRecord.FinishedUtc = DateTime.UtcNow;
        aRecord.Steps = string.Join(", ", theStepNames);

        await mySagaLogDbContext.SaveChangesAsync(theCancellationToken);
    }
}
