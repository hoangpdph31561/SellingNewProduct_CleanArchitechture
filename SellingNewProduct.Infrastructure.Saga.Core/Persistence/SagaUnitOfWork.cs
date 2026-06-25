using SellingNewProduct.Domain.Interfaces.Outbound;
using SellingNewProduct.Infrastructure.Saga.Core.Saga;

namespace SellingNewProduct.Infrastructure.Saga.Core.Persistence;

/// <summary>
/// The saga's implementation of the domain <see cref="IUnitOfWork"/> port. Begins a saga that
/// spans every registered <see cref="ISagaParticipant"/> (SQL + Mongo) instead of a single-database
/// transaction. The domain (e.g. <c>OrderWriteService</c>) keeps calling Begin/Commit/Rollback
/// exactly as before — only the implementation behind the port changes.
/// </summary>
internal sealed class SagaUnitOfWork : IUnitOfWork
{
    private readonly SagaContext myContext;
    private readonly IReadOnlyList<ISagaParticipant> myParticipants;
    private readonly ISagaLog myLog;

    public SagaUnitOfWork(
        SagaContext theContext,
        IEnumerable<ISagaParticipant> theParticipants,
        ISagaLog theLog)
    {
        myContext = theContext;
        myParticipants = theParticipants.ToList();
        myLog = theLog;
    }

    public async Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken theCancellationToken = default)
    {
        myContext.Begin();

        foreach (var aParticipant in myParticipants)
        {
            await aParticipant.BeginAsync(theCancellationToken);
        }

        await myLog.StartAsync(myContext.SagaId, theCancellationToken);
        return new SagaUnitOfWorkTransaction(myContext, myParticipants, myLog);
    }
}
