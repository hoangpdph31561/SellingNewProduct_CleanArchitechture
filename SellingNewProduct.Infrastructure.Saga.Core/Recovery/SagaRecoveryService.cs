using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SellingNewProduct.Infrastructure.Saga.Core.Saga;

namespace SellingNewProduct.Infrastructure.Saga.Core.Recovery;

/// <summary>
/// Closes the gap a saga cannot close with local commits alone: a crash mid-saga, after some steps
/// committed but before the saga finished. On startup it reads every unfinished saga from the single
/// <see cref="ISagaStore"/> and hands it to the same <see cref="SagaCompensator"/> the in-request
/// rollback uses, so an interrupted saga is undone exactly the way a live failure would be.
///
/// Runs once before serving traffic. Every compensation is idempotent, so a crash mid-recovery is
/// itself recoverable on the next start.
/// </summary>
public sealed class SagaRecoveryService : BackgroundService
{
    private readonly IServiceScopeFactory myScopeFactory;
    private readonly ILogger<SagaRecoveryService> myLogger;

    public SagaRecoveryService(IServiceScopeFactory theScopeFactory, ILogger<SagaRecoveryService> theLogger)
    {
        myScopeFactory = theScopeFactory;
        myLogger = theLogger;
    }

    protected override async Task ExecuteAsync(CancellationToken theStoppingToken)
    {
        try
        {
            await using var aScope = myScopeFactory.CreateAsyncScope();
            var aStore = aScope.ServiceProvider.GetRequiredService<ISagaStore>();
            var aCompensator = aScope.ServiceProvider.GetRequiredService<SagaCompensator>();

            var aUnfinished = await aStore.GetUnfinishedAsync(theStoppingToken);
            if (aUnfinished.Count == 0)
            {
                return;
            }

            myLogger.LogInformation("Saga recovery: reconciling {Count} unfinished saga(s).", aUnfinished.Count);

            foreach (var aSaga in aUnfinished)
            {
                try
                {
                    await aCompensator.CompensateAsync(aSaga, theStoppingToken);
                }
                catch (Exception aException)
                {
                    // One bad saga must not stop the rest; it stays in the store for the next start.
                    myLogger.LogError(aException, "Saga {SagaId}: recovery failed; will retry on next startup.", aSaga.SagaId);
                }
            }
        }
        catch (Exception aException)
        {
            // Never let recovery crash startup; the next start will retry (everything is idempotent).
            myLogger.LogError(aException, "Saga recovery failed; will retry on next startup.");
        }
    }
}
