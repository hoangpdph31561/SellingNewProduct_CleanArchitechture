namespace SellingNewProduct.Infrastructure.Saga.Core.Saga;

/// <summary>The information a compensation handler gets when asked to undo one step.</summary>
/// <param name="SagaId">The saga the step belongs to (so the handler can mark it compensated).</param>
/// <param name="StepName">The step being undone.</param>
/// <param name="CompensationData">The payload recorded when the step committed (e.g. stock deltas).</param>
public sealed record SagaCompensationContext(Guid SagaId, string StepName, string CompensationData);

/// <summary>
/// Knows how to undo ONE kind of step. Adding a new compensatable step to the saga means writing a
/// new handler and registering it — the orchestrator never changes.
///
/// CONTRACT: <see cref="CompensateAsync"/> must be IDEMPOTENT. Because the process can crash between
/// applying the undo and recording that it happened, a handler may be invoked more than once for the
/// same step; running it twice must not double-undo. The recommended way (used by the Mongo stock
/// handler) is to perform the revert and the "mark compensated" write atomically in one local
/// transaction, skipping if the step is already marked compensated.
/// </summary>
public interface ISagaCompensationHandler
{
    /// <summary>The <see cref="SagaStepInfo.CompensationType"/> this handler answers to.</summary>
    string CompensationType { get; }

    /// <summary>Undoes the step. Must be idempotent and should persist that the step is compensated.</summary>
    Task CompensateAsync(SagaCompensationContext theContext, CancellationToken theCancellationToken = default);
}

/// <summary>Resolves the handler responsible for a given <see cref="SagaStepInfo.CompensationType"/>.</summary>
public interface ISagaCompensationRegistry
{
    /// <summary>The handler for a compensation type, or null if none is registered.</summary>
    ISagaCompensationHandler? Resolve(string theCompensationType);
}
