using Microsoft.EntityFrameworkCore;
using SellingNewProduct.Domain.Orders;
using SellingNewProduct.Domain.Interfaces.Outbound;
using SellingNewProduct.Infrastructure.Saga.Core.Saga;
using SellingNewProduct.Infrastructure.SqlServer.Saga.Mapping;
using SellingNewProduct.Infrastructure.SqlServer.Saga.Persistence;

namespace SellingNewProduct.Infrastructure.SqlServer.Saga.Repositories.Write;

/// <summary>
/// SQL write side for orders. This is the saga's PIVOT step: each <c>SaveChanges</c> commits to SQL
/// immediately (commit-per-step). The moment the order write succeeds, the repository records a
/// <see cref="SagaStepKind.Pivot"/> marker in the ledger — the saga's point of no return. From then
/// on the saga is treated as successful and earlier steps are never undone (recovery finalizes it
/// instead of compensating). If the write throws BEFORE that marker, the orchestrator compensates the
/// earlier Mongo stock step. No saga state lives in SQL — the single ledger is in MongoDB.
/// </summary>
internal sealed class SqlOrderWriteRepository : IOrderWriteRepository
{
    private const string PivotStepName = "ConfirmOrder";

    private readonly AppDbContext myAppDbContext;
    private readonly SagaContext mySagaContext;
    private readonly ISagaStore mySagaStore;

    public SqlOrderWriteRepository(AppDbContext theAppDbContext, SagaContext theSagaContext, ISagaStore theSagaStore)
    {
        myAppDbContext = theAppDbContext;
        mySagaContext = theSagaContext;
        mySagaStore = theSagaStore;
    }

    public async Task<Order?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default)
    {
        var aRecord = await myAppDbContext.Orders
            .AsNoTracking()
            .Include(r => r.Details)
            .FirstOrDefaultAsync(r => r.Id == theId, theCancellationToken);

        return aRecord is null ? null : OrderMapper.ToDomain(aRecord);
    }

    public async Task AddAsync(Order theOrder, CancellationToken theCancellationToken = default)
    {
        myAppDbContext.Orders.Add(OrderMapper.ToRecord(theOrder));
        await myAppDbContext.SaveChangesAsync(theCancellationToken);
    }

    public async Task UpdateAsync(Order theOrder, CancellationToken theCancellationToken = default)
    {
        var aRecord = await myAppDbContext.Orders
            .Include(r => r.Details)
            .FirstOrDefaultAsync(r => r.Id == theOrder.Id, theCancellationToken);

        if (aRecord is null)
        {
            return;
        }

        OrderMapper.MapInto(aRecord, theOrder);
        await myAppDbContext.SaveChangesAsync(theCancellationToken);

        await MarkPivotCommittedAsync(theCancellationToken);
    }

    /// <summary>
    /// Records the pivot marker in the ledger right after the SQL order commit. Once it is there,
    /// recovery knows the saga reached the point of no return and must be finalized, not rolled back.
    /// </summary>
    private async Task MarkPivotCommittedAsync(CancellationToken theCancellationToken)
    {
        if (!mySagaContext.IsActive)
        {
            return; // Ship / cancel-draft and the like run outside a saga.
        }

        var aPivot = new SagaStepInfo(PivotStepName, SagaStepKind.Pivot, CompensationType: null, CompensationData: string.Empty, Compensated: false);
        await mySagaStore.EnrollStepAsync(mySagaContext.SagaId, aPivot, theCancellationToken);
    }
}
