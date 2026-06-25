namespace SellingNewProduct.Infrastructure.Saga.Core.Saga;

/// <summary>
/// Fallback saga log used when no persistent log is registered. Keeps the saga working (the
/// cross-database commits still happen and compensate) but records nothing. The SQL saga
/// infrastructure replaces this with a real, durable log.
/// </summary>
internal sealed class NullSagaLog : ISagaLog
{
    public Task StartAsync(Guid theSagaId, CancellationToken theCancellationToken = default)
        => Task.CompletedTask;

    public Task CompleteAsync(
        Guid theSagaId,
        SagaStatus theFinalStatus,
        IReadOnlyList<string> theStepNames,
        CancellationToken theCancellationToken = default)
        => Task.CompletedTask;
}
