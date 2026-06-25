using SellingNewProduct.Domain.Interfaces.Outbound;
using SellingNewProduct.Infrastructure.Saga.Core.Saga;

namespace SellingNewProduct.Infrastructure.Saga.Core.Persistence;

/// <summary>
/// The saga's implementation of the domain transaction handle. It fans <see cref="CommitAsync"/>
/// and <see cref="RollbackAsync"/> out to every <see cref="ISagaParticipant"/> and replays the
/// compensations recorded on the <see cref="SagaContext"/>.
///
/// Verb mapping for the domain <see cref="IUnitOfWorkTransaction"/> contract:
/// Begin = <see cref="SagaUnitOfWork.BeginTransactionAsync"/>, Commit = <see cref="CommitAsync"/>,
/// Rollback = <see cref="RollbackAsync"/>, End = <see cref="DisposeAsync"/> (via <c>await using</c>).
/// </summary>
internal sealed class SagaUnitOfWorkTransaction : IUnitOfWorkTransaction
{
    private readonly SagaContext myContext;
    private readonly IReadOnlyList<ISagaParticipant> myParticipants;
    private readonly ISagaLog myLog;
    private bool myFinished;

    public SagaUnitOfWorkTransaction(
        SagaContext theContext,
        IReadOnlyList<ISagaParticipant> theParticipants,
        ISagaLog theLog)
    {
        myContext = theContext;
        myParticipants = theParticipants;
        myLog = theLog;
    }

    /// <summary>
    /// Commits the saga. Non-pivot stores (Mongo) commit FIRST, the pivot (SQL) commits LAST:
    /// <list type="bullet">
    /// <item>If a non-pivot commit fails, the pivot is still pending — the caller's
    /// <see cref="RollbackAsync"/> discards everything natively and no compensation is needed.</item>
    /// <item>If the pivot commit fails after a non-pivot already committed, the exception propagates;
    /// the caller's <see cref="RollbackAsync"/> replays the compensations to undo that committed store.</item>
    /// </list>
    /// </summary>
    public async Task CommitAsync(CancellationToken theCancellationToken = default)
    {
        if (myFinished)
        {
            return;
        }

        foreach (var aParticipant in myParticipants.Where(p => !p.IsPivot))
        {
            await aParticipant.CommitAsync(theCancellationToken);
        }

        foreach (var aParticipant in myParticipants.Where(p => p.IsPivot))
        {
            await aParticipant.CommitAsync(theCancellationToken);
        }

        myContext.MarkCommitted();
        myFinished = true;
        await myLog.CompleteAsync(myContext.SagaId, SagaStatus.Committed, myContext.StepNames, theCancellationToken);
    }

    /// <summary>
    /// Undoes the saga: discards every still-pending local transaction natively, then replays the
    /// registered compensations in reverse to revert any store that had ALREADY committed (the
    /// Mongo side, when the SQL pivot's commit failed after it).
    /// </summary>
    public async Task RollbackAsync(CancellationToken theCancellationToken = default)
    {
        if (myFinished)
        {
            return;
        }

        myFinished = true;
        myContext.MarkCompensating();

        // Discard pending local work natively. A store that already committed has nulled its
        // transaction, so its RollbackAsync is a no-op — the compensation below undoes it instead.
        foreach (var aParticipant in myParticipants)
        {
            await SafeAsync(() => aParticipant.RollbackAsync(theCancellationToken));
        }

        // Replay compensations in reverse: the last local commit is undone first.
        var aCompensated = true;
        for (var anIndex = myContext.Compensations.Count - 1; anIndex >= 0; anIndex--)
        {
            var aCompensation = myContext.Compensations[anIndex];
            try
            {
                await aCompensation.CompensateAsync(theCancellationToken);
            }
            catch
            {
                // A failing compensation leaves data needing manual reconciliation; record it
                // and keep going so the remaining steps still get their chance to undo.
                aCompensated = false;
            }
        }

        if (aCompensated)
        {
            myContext.MarkCompensated();
            await myLog.CompleteAsync(myContext.SagaId, SagaStatus.Compensated, myContext.StepNames, theCancellationToken);
        }
        else
        {
            myContext.MarkFailed();
            await myLog.CompleteAsync(myContext.SagaId, SagaStatus.Failed, myContext.StepNames, theCancellationToken);
        }
    }

    /// <summary>End: if neither commit nor rollback ran, the scope is unwinding abnormally — undo.</summary>
    public async ValueTask DisposeAsync()
    {
        if (!myFinished)
        {
            await RollbackAsync();
        }

        foreach (var aParticipant in myParticipants)
        {
            if (aParticipant is IAsyncDisposable aDisposable)
            {
                await SafeAsync(async () => await aDisposable.DisposeAsync());
            }
        }
    }

    private static async Task SafeAsync(Func<Task> theAction)
    {
        try
        {
            await theAction();
        }
        catch
        {
            // Best-effort cleanup: a participant that already failed its commit may throw on
            // rollback/dispose. Swallow so one broken participant cannot mask the original error.
        }
    }
}
