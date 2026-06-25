namespace SellingNewProduct.Infrastructure.Saga.Core.Saga;

/// <summary>
/// Persists the outcome of each saga so commits across the two databases can be tracked and
/// audited after the fact. The implementation MUST write outside the saga's own transaction
/// (its own connection/context) — otherwise a compensated saga would roll its own log row away.
/// </summary>
public interface ISagaLog
{
    /// <summary>Records that a saga has started (status <see cref="SagaStatus.Started"/>).</summary>
    Task StartAsync(Guid theSagaId, CancellationToken theCancellationToken = default);

    /// <summary>Records the final outcome of a saga and the steps that participated.</summary>
    Task CompleteAsync(
        Guid theSagaId,
        SagaStatus theFinalStatus,
        IReadOnlyList<string> theStepNames,
        CancellationToken theCancellationToken = default);
}
