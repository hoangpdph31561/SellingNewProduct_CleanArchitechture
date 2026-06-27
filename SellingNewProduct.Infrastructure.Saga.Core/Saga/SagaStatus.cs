namespace SellingNewProduct.Infrastructure.Saga.Core.Saga;

/// <summary>
/// Lifecycle of an orchestration saga (a cross-database "transaction" made of per-step local
/// commits + compensations). Persisted on the saga instance so an operator — or the startup
/// recovery worker — can see exactly where a saga stopped and what still has to happen.
/// </summary>
public enum SagaStatus
{
    /// <summary>No saga is in progress.</summary>
    NotStarted = 0,

    /// <summary>A saga has begun; steps are committing to their own databases one at a time.</summary>
    Started = 1,

    /// <summary>Every step committed and the saga finished successfully (the happy path).</summary>
    Committed = 2,

    /// <summary>A step failed; the recorded compensations are being replayed in reverse.</summary>
    Compensating = 3,

    /// <summary>All compensations finished — every earlier local commit has been undone.</summary>
    Compensated = 4,

    /// <summary>A compensation kept failing past the retry budget; the saga needs manual reconciliation.</summary>
    NeedsManualReview = 5,
}
