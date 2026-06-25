namespace SellingNewProduct.Infrastructure.Saga.Core.Saga;

/// <summary>
/// The per-request, mutable state of one saga. Registered as <c>scoped</c> so that every
/// participant resolved in the same DI scope — the SQL transaction wrapper, the Mongo
/// repositories, the unit of work — shares ONE instance and therefore one saga.
///
/// A saga replaces the (impossible) distributed ACID transaction across SQL Server + MongoDB
/// with a sequence of local commits: each step commits to its own database immediately and
/// registers a <see cref="SagaCompensation"/> that knows how to undo it. If a later step fails,
/// the unit of work replays those compensations in reverse — that is the "rollback" of a saga.
/// </summary>
public sealed class SagaContext
{
    private readonly List<SagaCompensation> myCompensations = new();

    /// <summary>Identifies this saga across the log and any diagnostics.</summary>
    public Guid SagaId { get; private set; }

    public SagaStatus Status { get; private set; } = SagaStatus.NotStarted;

    /// <summary>True while a saga is in flight, so saga-aware repositories know to snapshot + register compensation.</summary>
    public bool IsActive => Status == SagaStatus.Started;

    public IReadOnlyList<SagaCompensation> Compensations => myCompensations;

    /// <summary>Names of the steps that registered a compensation — recorded in the saga log.</summary>
    public IReadOnlyList<string> StepNames => myCompensations.Select(c => c.StepName).ToList();

    /// <summary>Opens a fresh saga: new id, cleared compensations, active status.</summary>
    public void Begin()
    {
        SagaId = Guid.NewGuid();
        myCompensations.Clear();
        Status = SagaStatus.Started;
    }

    /// <summary>
    /// Records how to undo a local commit that just succeeded. The compensation runs only if a
    /// LATER step fails. Steps are compensated in reverse registration order.
    /// </summary>
    public void RegisterCompensation(string theStepName, Func<CancellationToken, Task> theCompensation)
    {
        if (!IsActive)
        {
            return;
        }

        myCompensations.Add(new SagaCompensation(theStepName, theCompensation));
    }

    public void MarkCommitted() => Status = SagaStatus.Committed;

    public void MarkCompensating() => Status = SagaStatus.Compensating;

    public void MarkCompensated() => Status = SagaStatus.Compensated;

    public void MarkFailed() => Status = SagaStatus.Failed;
}

/// <summary>A single undo action plus the human-readable name of the step it reverses.</summary>
public sealed record SagaCompensation(string StepName, Func<CancellationToken, Task> CompensateAsync);
