namespace SellingNewProduct.Infrastructure.Saga.Core.Saga;

/// <summary>
/// The single, durable saga ledger — the one place every saga is recorded. Replaces the scattered
/// per-database markers of the old pivot design with ONE store (backed by MongoDB here). It is the
/// source of truth for rollback and for the startup recovery worker: if the process dies mid-saga,
/// the unfinished instances and their compensation data are still here to be replayed.
///
/// The implementation MUST persist each call durably and on its own (it does not enlist in any
/// business transaction), so the ledger survives even when a business commit is later compensated.
/// </summary>
public interface ISagaStore
{
    /// <summary>Records that a saga has begun (status <see cref="SagaStatus.Started"/>, no steps yet).</summary>
    Task StartAsync(Guid theSagaId, string theName, CancellationToken theCancellationToken = default);

    /// <summary>
    /// Appends a step that has just committed to its database, together with the data needed to
    /// undo it. Durable on return — a crash after this still leaves the step recoverable.
    /// </summary>
    Task EnrollStepAsync(Guid theSagaId, SagaStepInfo theStep, CancellationToken theCancellationToken = default);

    /// <summary>Marks one step's compensation as done, so it is not replayed twice.</summary>
    Task MarkStepCompensatedAsync(Guid theSagaId, string theStepName, CancellationToken theCancellationToken = default);

    /// <summary>Sets the saga's overall status (e.g. Compensating, NeedsManualReview).</summary>
    Task SetStatusAsync(Guid theSagaId, SagaStatus theStatus, CancellationToken theCancellationToken = default);

    /// <summary>Increments the compensation retry counter; returns the new value.</summary>
    Task<int> IncrementRetryAsync(Guid theSagaId, CancellationToken theCancellationToken = default);

    /// <summary>Reads one saga, or null if it has already been removed.</summary>
    Task<SagaSnapshot?> LoadAsync(Guid theSagaId, CancellationToken theCancellationToken = default);

    /// <summary>Every saga that has not reached a terminal state — the recovery candidates.</summary>
    Task<IReadOnlyList<SagaSnapshot>> GetUnfinishedAsync(CancellationToken theCancellationToken = default);

    /// <summary>Deletes a saga once it has fully committed or fully compensated.</summary>
    Task RemoveAsync(Guid theSagaId, CancellationToken theCancellationToken = default);
}
