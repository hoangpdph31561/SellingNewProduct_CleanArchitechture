using Microsoft.Extensions.Logging;

namespace SellingNewProduct.Infrastructure.Saga.Core.Saga;

/// <summary>
/// The undo half of the orchestrator, shared by the in-request rollback path
/// (<c>SagaUnitOfWorkTransaction.RollbackAsync</c>) and the startup <c>SagaRecoveryService</c> — both
/// undo a saga the same way, so the logic lives here once.
///
/// It replays every <see cref="SagaStepKind.Compensatable"/> step in REVERSE order. A compensation
/// is retried up to <see cref="MaxRetries"/> times (the saga spirit: "roll back until it sticks").
/// If a step still cannot be undone, the whole saga is parked in <see cref="SagaStatus.NeedsManualReview"/>
/// rather than silently dropped. Pivot and forward-only steps are skipped — they are never undone.
/// </summary>
public sealed class SagaCompensator
{
    private const int MaxRetries = 5;

    private readonly ISagaStore myStore;
    private readonly ISagaCompensationRegistry myRegistry;
    private readonly ILogger<SagaCompensator> myLogger;

    public SagaCompensator(
        ISagaStore theStore,
        ISagaCompensationRegistry theRegistry,
        ILogger<SagaCompensator> theLogger)
    {
        myStore = theStore;
        myRegistry = theRegistry;
        myLogger = theLogger;
    }

    /// <summary>
    /// Compensates one saga. Returns true when every compensatable step was undone (the saga is then
    /// removed); false when a step exhausted its retries and the saga was parked for manual review.
    /// </summary>
    public async Task<bool> CompensateAsync(SagaSnapshot theSaga, CancellationToken theCancellationToken = default)
    {
        // Point-of-no-return guard: if a pivot step was recorded, the saga already passed the point
        // where it is considered successful (the SQL order committed). It must NOT be rolled back —
        // recovery just finalizes it. This closes the crash window between the pivot commit and the
        // happy-path cleanup, where the ledger still looks "Started".
        if (theSaga.Steps.Any(s => s.Kind == SagaStepKind.Pivot))
        {
            await myStore.SetStatusAsync(theSaga.SagaId, SagaStatus.Committed, theCancellationToken);
            await myStore.RemoveAsync(theSaga.SagaId, theCancellationToken);
            myLogger.LogInformation("Saga {SagaId}: pivot already committed; finalized as committed.", theSaga.SagaId);
            return true;
        }

        await myStore.SetStatusAsync(theSaga.SagaId, SagaStatus.Compensating, theCancellationToken);

        // Reverse order: the most recent local commit is undone first.
        for (var anIndex = theSaga.Steps.Count - 1; anIndex >= 0; anIndex--)
        {
            var aStep = theSaga.Steps[anIndex];

            if (aStep.Kind != SagaStepKind.Compensatable || aStep.Compensated)
            {
                continue; // Pivot / forward-only / already undone — nothing to do.
            }

            if (!await TryCompensateStepAsync(theSaga.SagaId, aStep, theCancellationToken))
            {
                await myStore.SetStatusAsync(theSaga.SagaId, SagaStatus.NeedsManualReview, theCancellationToken);
                myLogger.LogError(
                    "Saga {SagaId}: step '{Step}' could not be compensated after {Max} attempts; parked for manual review.",
                    theSaga.SagaId, aStep.Name, MaxRetries);
                return false;
            }
        }

        await myStore.SetStatusAsync(theSaga.SagaId, SagaStatus.Compensated, theCancellationToken);
        await myStore.RemoveAsync(theSaga.SagaId, theCancellationToken);
        return true;
    }
    private async Task<bool> TryCompensateStepAsync(Guid theSagaId, SagaStepInfo theStep, CancellationToken theCancellationToken)
    {
        if (theStep.CompensationType is null)
        {
            return true; // Marked compensatable but carries no undo — treat as nothing to undo.
        }

        var aHandler = myRegistry.Resolve(theStep.CompensationType);
        if (aHandler is null)
        {
            myLogger.LogError(
                "Saga {SagaId}: no compensation handler registered for type '{Type}'.",
                theSagaId, theStep.CompensationType);
            return false;
        }

        var aContext = new SagaCompensationContext(theSagaId, theStep.Name, theStep.CompensationData);

        for (var anAttempt = 1; anAttempt <= MaxRetries; anAttempt++)
        {
            try
            {
                await aHandler.CompensateAsync(aContext, theCancellationToken);
                return true;
            }
            catch (Exception aException) when (anAttempt < MaxRetries)
            {
                var aRetry = await myStore.IncrementRetryAsync(theSagaId, theCancellationToken);
                myLogger.LogWarning(
                    aException,
                    "Saga {SagaId}: compensation '{Step}' failed (attempt {Attempt}/{Max}, total retries {Total}); retrying.",
                    theSagaId, theStep.Name, anAttempt, MaxRetries, aRetry);

                await Task.Delay(Backoff(anAttempt), theCancellationToken);
            }
            catch (Exception aException)
            {
                myLogger.LogError(
                    aException,
                    "Saga {SagaId}: compensation '{Step}' failed on the final attempt.",
                    theSagaId, theStep.Name);
            }
        }

        return false;
    }

    /// <summary>Exponential-ish backoff capped at a few seconds, so retries do not hammer the store.</summary>
    private static TimeSpan Backoff(int theAttempt)
        => TimeSpan.FromMilliseconds(Math.Min(200 * Math.Pow(2, theAttempt - 1), 5000));
}
