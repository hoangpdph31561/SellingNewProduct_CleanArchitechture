using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SellingNewProduct.Infrastructure.Saga.Core.Saga;

namespace SellingNewProduct.Infrastructure.Saga.Core.Recovery;

/// <summary>
/// Closes the one residual gap a saga cannot close with transactions alone: a crash BETWEEN the two
/// commits (Mongo committed, the SQL pivot not yet). On startup it reconciles every saga whose Mongo
/// effect is still pending, using the durable markers as the source of truth:
/// <list type="bullet">
/// <item>Mongo effect exists AND SQL commit marker exists → both committed → consistent → clean up.</item>
/// <item>Mongo effect exists but NO SQL commit marker → interrupted → revert the Mongo stock deltas.</item>
/// </list>
/// Runs once at startup, before serving traffic. Every action is idempotent, so a crash mid-recovery
/// is itself recoverable on the next start.
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
            var aEffectStore = aScope.ServiceProvider.GetRequiredService<ISagaEffectStore>();
            var aCommitStore = aScope.ServiceProvider.GetRequiredService<ISagaCommitStore>();
            var aLog = aScope.ServiceProvider.GetRequiredService<ISagaLog>();

            var aPending = await aEffectStore.GetPendingSagaIdsAsync(theStoppingToken);
            if (aPending.Count == 0)
            {
                return;
            }

            myLogger.LogInformation("Saga recovery: reconciling {Count} pending saga(s).", aPending.Count);

            foreach (var aSagaId in aPending)
            {
                await ReconcileAsync(aSagaId, aEffectStore, aCommitStore, aLog, theStoppingToken);
            }
        }
        catch (Exception aException)
        {
            // Never let recovery crash startup; the next start will retry (everything is idempotent).
            myLogger.LogError(aException, "Saga recovery failed; will retry on next startup.");
        }
    }

    private async Task ReconcileAsync(
        Guid theSagaId,
        ISagaEffectStore theEffectStore,
        ISagaCommitStore theCommitStore,
        ISagaLog theLog,
        CancellationToken theCancellationToken)
    {
        try
        {
            if (await theCommitStore.ExistsAsync(theSagaId, theCancellationToken))
            {
                // Pivot committed too → the saga actually succeeded → just drop the markers.
                await theEffectStore.RemoveAsync(theSagaId, theCancellationToken);
                await theCommitStore.RemoveAsync(theSagaId, theCancellationToken);
                await theLog.CompleteAsync(theSagaId, SagaStatus.Committed, Array.Empty<string>(), theCancellationToken);
                myLogger.LogInformation("Saga {SagaId}: confirmed committed on both stores.", theSagaId);
            }
            else
            {
                // Mongo committed but the SQL pivot did not → undo the orphaned stock change.
                await theEffectStore.RevertAsync(theSagaId, theCancellationToken);
                await theLog.CompleteAsync(theSagaId, SagaStatus.Compensated, Array.Empty<string>(), theCancellationToken);
                myLogger.LogWarning("Saga {SagaId}: pivot never committed; Mongo effect reverted.", theSagaId);
            }
        }
        catch (Exception aException)
        {
            myLogger.LogError(aException, "Saga {SagaId}: recovery step failed; will retry on next startup.", theSagaId);
        }
    }
}
