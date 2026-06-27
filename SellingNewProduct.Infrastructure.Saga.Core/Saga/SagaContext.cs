namespace SellingNewProduct.Infrastructure.Saga.Core.Saga;

/// <summary>
/// The per-request, mutable state of one saga. Registered as <c>scoped</c> so every component in the
/// same DI scope — the unit of work, the saga-aware repositories, the store — shares ONE instance and
/// therefore agrees on which saga (which <see cref="SagaId"/>) is in flight.
///
/// Unlike the old design, this holds NO compensation list in memory: each step's undo information is
/// persisted to <see cref="ISagaStore"/> the moment the step commits, so a crash cannot lose it.
/// The context only carries the identity and the "is a saga running right now?" flag that saga-aware
/// repositories check before they enroll a step.
/// </summary>
public sealed class SagaContext
{
    /// <summary>Identifies this saga across the store, the logs and any diagnostics.</summary>
    public Guid SagaId { get; private set; }

    /// <summary>The saga type, e.g. <c>"OrderWrite"</c>. Recorded on the instance for auditing.</summary>
    public string Name { get; private set; } = "Saga";

    public SagaStatus Status { get; private set; } = SagaStatus.NotStarted;

    /// <summary>True while a saga is in flight, so saga-aware repositories know to commit-and-enroll.</summary>
    public bool IsActive => Status == SagaStatus.Started;

    /// <summary>Opens a fresh saga: new id, active status.</summary>
    public void Begin(string theName)
    {
        SagaId = Guid.NewGuid();
        Name = theName;
        Status = SagaStatus.Started;
    }

    public void MarkCommitted() => Status = SagaStatus.Committed;

    public void MarkCompensating() => Status = SagaStatus.Compensating;

    public void MarkCompensated() => Status = SagaStatus.Compensated;

    public void MarkNeedsManualReview() => Status = SagaStatus.NeedsManualReview;
}
