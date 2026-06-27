using SellingNewProduct.Domain.Interfaces.Outbound;
using SellingNewProduct.Infrastructure.Saga.Core.Saga;

namespace SellingNewProduct.Infrastructure.Saga.Core.Persistence;

/// <summary>
/// The saga's implementation of the domain transaction handle. By the time this runs, each step has
/// ALREADY committed to its own database and recorded itself in the <see cref="ISagaStore"/> — so
/// there is nothing left to "commit" here:
///
/// <list type="bullet">
/// <item><b>Commit</b> — the happy path. Every step succeeded, so mark the saga committed and drop
/// its record from the store.</item>
/// <item><b>Rollback</b> — a step threw. Load the saga and hand it to the <see cref="SagaCompensator"/>,
/// which replays the recorded compensations in reverse, retrying until they stick.</item>
/// <item><b>End</b> — <see cref="DisposeAsync"/>. If the scope unwinds without an explicit outcome,
/// roll back.</item>
/// </list>
/// </summary>
internal sealed class SagaUnitOfWorkTransaction : IUnitOfWorkTransaction
{
    private readonly SagaContext myContext;
    private readonly ISagaStore myStore;
    private readonly SagaCompensator myCompensator;
    private bool myFinished;

    public SagaUnitOfWorkTransaction(SagaContext theContext, ISagaStore theStore, SagaCompensator theCompensator)
    {
        myContext = theContext;
        myStore = theStore;
        myCompensator = theCompensator;
    }

    public async Task CommitAsync(CancellationToken theCancellationToken = default)
    {
        if (myFinished)
        {
            return;
        }

        myFinished = true;
        myContext.MarkCommitted();

        // Every step already committed to its own database; the saga succeeded, so just drop the
        // ledger entry. (A surviving entry is exactly what tells recovery a saga did NOT finish.)
        await myStore.RemoveAsync(myContext.SagaId, theCancellationToken);
    }

    public async Task RollbackAsync(CancellationToken theCancellationToken = default)
    {
        if (myFinished)
        {
            return;
        }

        myFinished = true;
        myContext.MarkCompensating();

        var aSaga = await myStore.LoadAsync(myContext.SagaId, theCancellationToken);
        if (aSaga is null)
        {
            return; // Nothing was recorded (saga failed before any step committed) — nothing to undo.
        }

        var aCompensated = await myCompensator.CompensateAsync(aSaga, theCancellationToken);
        if (aCompensated)
        {
            myContext.MarkCompensated();
        }
        else
        {
            // The compensator already parked the saga in NeedsManualReview and logged it; the startup
            // recovery worker will keep retrying it on the next run.
            myContext.MarkNeedsManualReview();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (!myFinished)
        {
            await RollbackAsync();
        }
    }
}
