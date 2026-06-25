namespace SellingNewProduct.Infrastructure.Saga.Core.Saga;

/// <summary>
/// Lifecycle of a saga (a cross-database "transaction" made of local commits + compensations).
/// Recorded in the saga log so an operator can see, after the fact, whether every database
/// committed (<see cref="Committed"/>) or an earlier step had to be undone (<see cref="Compensated"/>).
/// </summary>
public enum SagaStatus
{
    /// <summary>No saga is in progress on this scope.</summary>
    NotStarted = 0,

    /// <summary>A saga has begun; steps may be writing to their local databases.</summary>
    Started = 1,

    /// <summary>Every participant committed its local transaction — the happy path.</summary>
    Committed = 2,

    /// <summary>A step failed; registered compensations are being replayed in reverse.</summary>
    Compensating = 3,

    /// <summary>Compensations finished — earlier local commits have been undone.</summary>
    Compensated = 4,

    /// <summary>A compensation itself failed; the data may need manual reconciliation.</summary>
    Failed = 5,
}
