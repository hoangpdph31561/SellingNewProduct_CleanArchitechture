using Microsoft.Extensions.Logging;
using SellingNewProduct.Domain.Interfaces.Outbound;
using SellingNewProduct.Infrastructure.Saga.Core.Saga;

namespace SellingNewProduct.Infrastructure.Saga.Core.Persistence;

/// <summary>
/// The saga's implementation of the domain <see cref="IUnitOfWork"/> port. The domain (e.g.
/// <c>OrderWriteService</c>) keeps calling Begin/Commit/Rollback exactly as before — only the meaning
/// behind the port changes: instead of one database transaction, this opens an ORCHESTRATION SAGA.
///
/// Unlike the old pivot design there are no held-open transactions here. Each saga-aware repository
/// commits to its own database immediately when its step runs and records that step (with its undo
/// data) in the single <see cref="ISagaStore"/>. Begin just opens the saga; the returned handle
/// finalizes it (Commit) or replays the recorded compensations (Rollback).
/// </summary>
internal sealed class SagaUnitOfWork : IUnitOfWork
{
    private readonly SagaContext myContext;
    private readonly ISagaStore myStore;
    private readonly SagaCompensator myCompensator;
    private readonly ILogger<SagaUnitOfWork> myLogger;
    private readonly ILogger<SagaUnitOfWorkTransaction> myTransactionLogger;

    public SagaUnitOfWork(
        SagaContext theContext,
        ISagaStore theStore,
        SagaCompensator theCompensator,
        ILogger<SagaUnitOfWork> theLogger,
        ILogger<SagaUnitOfWorkTransaction> theTransactionLogger)
    {
        myContext = theContext;
        myStore = theStore;
        myCompensator = theCompensator;
        myLogger = theLogger;
        myTransactionLogger = theTransactionLogger;
    }

    public async Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken theCancellationToken = default)
    {
        myContext.Begin("OrderWrite");
        await myStore.StartAsync(myContext.SagaId, myContext.Name, theCancellationToken);
        myLogger.LogInformation("Saga {SagaId} '{Name}' started.", myContext.SagaId, myContext.Name);
        return new SagaUnitOfWorkTransaction(myContext, myStore, myCompensator, myTransactionLogger);
    }
}
