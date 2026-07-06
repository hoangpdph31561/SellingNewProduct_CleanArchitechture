using Microsoft.EntityFrameworkCore;
using SellingNewProduct.Domain.Orders;
using SellingNewProduct.Domain.Interfaces.Outbound;
using SellingNewProduct.Infrastructure.Saga.Core.Saga;
using SellingNewProduct.Infrastructure.SqlServer.Saga.Mapping;
using SellingNewProduct.Infrastructure.SqlServer.Saga.Outbox;
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
    private const string PublishStepName = "PublishOrderEvents";

    private readonly AppDbContext myAppDbContext;
    private readonly SagaContext mySagaContext;
    private readonly ISagaStore mySagaStore;
    private readonly OutboxWriter myOutboxWriter;

    public SqlOrderWriteRepository(
        AppDbContext theAppDbContext, SagaContext theSagaContext, ISagaStore theSagaStore, OutboxWriter theOutboxWriter)
    {
        myAppDbContext = theAppDbContext;
        mySagaContext = theSagaContext;
        mySagaStore = theSagaStore;
        myOutboxWriter = theOutboxWriter;
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

        // Stage the OrderPlaced event in the SAME commit as the new order (transactional outbox).
        myOutboxWriter.Stage(theOrder);
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

        // The order change AND the events it raised (Confirmed/Shipped/Cancelled) commit together.
        var aPublished = myOutboxWriter.Stage(theOrder);
        await myAppDbContext.SaveChangesAsync(theCancellationToken);

        await MarkPivotCommittedAsync(theCancellationToken);
        await RecordPublishStepAsync(aPublished, theCancellationToken);
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

    /// <summary>
    /// Records the event publication as a <see cref="SagaStepKind.RetryableForward"/> step — a
    /// non-undoable side effect that runs AFTER the pivot. The outbox row is already committed, so
    /// this is purely for the saga's audit trail: it documents that, past the point of no return, the
    /// saga's job is to keep pushing forward (publish the events), never to roll back.
    /// </summary>
    private async Task RecordPublishStepAsync(int thePublishedCount, CancellationToken theCancellationToken)
    {
        if (!mySagaContext.IsActive || thePublishedCount == 0)
        {
            return;
        }

        var aStep = new SagaStepInfo(
            PublishStepName, SagaStepKind.RetryableForward, CompensationType: null, CompensationData: string.Empty, Compensated: false);
        await mySagaStore.EnrollStepAsync(mySagaContext.SagaId, aStep, theCancellationToken);
    }
}
