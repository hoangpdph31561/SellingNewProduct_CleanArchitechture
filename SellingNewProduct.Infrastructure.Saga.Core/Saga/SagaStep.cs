namespace SellingNewProduct.Infrastructure.Saga.Core.Saga;

/// <summary>
/// Classifies a saga step so the orchestrator knows how to treat it when something goes wrong.
/// This is the seam that lets the saga scale to new steps (e.g. update-user-history, send-email)
/// without touching the orchestrator: a new step only has to pick the right kind.
/// </summary>
public enum SagaStepKind
{
    /// <summary>
    /// Has a real undo action. Runs BEFORE the pivot. On failure the orchestrator replays its
    /// compensation (e.g. revert a stock change, delete a history row).
    /// </summary>
    Compensatable = 0,

    /// <summary>
    /// The point of no return — once it commits, the saga is considered successful and earlier
    /// steps are never undone. Exactly one step should be the pivot (the SQL Order/Payment write).
    /// </summary>
    Pivot = 1,

    /// <summary>
    /// A non-undoable side effect (e.g. send an email). Runs AFTER the pivot and is never rolled
    /// back — on failure it is retried forward instead. Recorded for audit; ignored by compensation.
    /// </summary>
    RetryableForward = 2,
}

/// <summary>One step recorded on a saga: what it was, how to undo it, and whether it already has been.</summary>
/// <param name="Name">Human-readable step name, e.g. <c>"DecreaseStock"</c>.</param>
/// <param name="Kind">How the orchestrator treats the step on rollback.</param>
/// <param name="CompensationType">
/// Key used to resolve the compensation handler from <see cref="ISagaCompensationRegistry"/>.
/// Null for steps that need no compensation (pivot / forward-only).
/// </param>
/// <param name="CompensationData">Opaque payload the handler needs to undo the step (serialized).</param>
/// <param name="Compensated">True once this step's compensation has run successfully.</param>
public sealed record SagaStepInfo(
    string Name,
    SagaStepKind Kind,
    string? CompensationType,
    string CompensationData,
    bool Compensated);

/// <summary>A point-in-time read of a persisted saga, used by rollback and crash recovery.</summary>
public sealed record SagaSnapshot(
    Guid SagaId,
    string Name,
    SagaStatus Status,
    IReadOnlyList<SagaStepInfo> Steps,
    int RetryCount);
