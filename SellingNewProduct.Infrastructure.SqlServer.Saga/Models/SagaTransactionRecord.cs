namespace SellingNewProduct.Infrastructure.SqlServer.Saga.Models;

/// <summary>
/// Durable record of one saga: when it started, how it ended (Committed / Compensated / Failed)
/// and which steps participated. This is the "track commit" ledger — it lets an operator confirm,
/// after the fact, that a cross-database operation either fully committed or was fully undone.
/// Lives in its own table written by its own context, so a compensated saga still keeps its log.
/// </summary>
internal sealed class SagaTransactionRecord
{
    public Guid Id { get; set; }
    public int Status { get; set; }
    public DateTime StartedUtc { get; set; }
    public DateTime? FinishedUtc { get; set; }
    public string Steps { get; set; } = string.Empty;
}
