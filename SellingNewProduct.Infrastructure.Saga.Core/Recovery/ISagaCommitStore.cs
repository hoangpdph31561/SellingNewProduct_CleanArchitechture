namespace SellingNewProduct.Infrastructure.Saga.Core.Recovery;

/// <summary>
/// Durable proof that the SQL pivot committed for a given saga. The marker is written INSIDE the
/// pivot's own business transaction, so "a row exists for SagaId" is atomically equivalent to "the
/// pivot committed". Recovery uses this as the source of truth: if the Mongo side committed (a
/// <see cref="ISagaEffectStore"/> effect exists) but no commit marker exists, the saga was
/// interrupted between the two commits and the Mongo effect must be reverted.
/// </summary>
public interface ISagaCommitStore
{
    Task<bool> ExistsAsync(Guid theSagaId, CancellationToken theCancellationToken = default);

    Task RemoveAsync(Guid theSagaId, CancellationToken theCancellationToken = default);
}
