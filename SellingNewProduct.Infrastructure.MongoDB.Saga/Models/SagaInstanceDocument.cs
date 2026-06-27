namespace SellingNewProduct.Infrastructure.MongoDB.Saga.Models;

/// <summary>
/// The single durable saga ledger entry, stored in the <c>saga_instances</c> collection — the one
/// place every saga lives (decision: keep the log in MongoDB). It replaces the three scattered
/// markers of the old pivot design (SQL saga-log table, SQL commit marker, Mongo effect document).
///
/// Each step the saga commits is appended to <see cref="Steps"/> together with the data needed to
/// undo it, so rollback and crash-recovery can replay compensations from this document alone.
/// </summary>
internal sealed class SagaInstanceDocument
{
    public Guid Id { get; set; }                 // = SagaId
    public string Name { get; set; } = string.Empty;
    public int Status { get; set; }
    public int RetryCount { get; set; }
    public DateTime StartedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public List<SagaStepDocument> Steps { get; set; } = new();
}

/// <summary>One committed step on a saga: what it was, how to undo it, and whether it has been.</summary>
internal sealed class SagaStepDocument
{
    public string Name { get; set; } = string.Empty;
    public int Kind { get; set; }
    public string? CompensationType { get; set; }
    public string CompensationData { get; set; } = string.Empty;
    public bool Compensated { get; set; }
}
