using Microsoft.EntityFrameworkCore;
using SellingNewProduct.Infrastructure.MongoDB.Saga.Models;
using SellingNewProduct.Infrastructure.MongoDB.Saga.Persistence;
using SellingNewProduct.Infrastructure.Saga.Core.Saga;

namespace SellingNewProduct.Infrastructure.MongoDB.Saga.Saga;

/// <summary>
/// MongoDB-backed implementation of the single saga ledger (<see cref="ISagaStore"/>), stored in the
/// <c>saga_instances</c> collection. It shares the scoped <see cref="MongoAppDbContext"/> with the
/// saga-aware Mongo repositories, which is what lets a Mongo step record itself ATOMICALLY with its
/// own stock change: the repository stages the step via <see cref="StageStepAsync"/> (no save) and
/// then commits both in a single Mongo transaction.
/// </summary>
internal sealed class MongoSagaStore : ISagaStore
{
    private readonly MongoAppDbContext myContext;

    public MongoSagaStore(MongoAppDbContext theContext)
    {
        myContext = theContext;
    }

    public async Task StartAsync(Guid theSagaId, string theName, CancellationToken theCancellationToken = default)
    {
        myContext.SagaInstances.Add(new SagaInstanceDocument
        {
            Id = theSagaId,
            Name = theName,
            Status = (int)SagaStatus.Started,
            RetryCount = 0,
            StartedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow,
            Steps = new List<SagaStepDocument>()
        });

        await myContext.SaveChangesAsync(theCancellationToken);
    }

    /// <summary>
    /// Stages a step onto the shared context WITHOUT saving, so the caller's own
    /// <c>SaveChanges</c>/commit persists the step atomically with its business write. Used by the
    /// Mongo write repositories inside their own transaction.
    /// </summary>
    public async Task StageStepAsync(Guid theSagaId, SagaStepInfo theStep, CancellationToken theCancellationToken = default)
    {
        var aDocument = await LoadTrackedAsync(theSagaId, theCancellationToken);
        if (aDocument is null)
        {
            return;
        }

        aDocument.Steps.Add(ToDocument(theStep));
        aDocument.UpdatedUtc = DateTime.UtcNow;
    }

    public async Task EnrollStepAsync(Guid theSagaId, SagaStepInfo theStep, CancellationToken theCancellationToken = default)
    {
        await StageStepAsync(theSagaId, theStep, theCancellationToken);
        await myContext.SaveChangesAsync(theCancellationToken);
    }

    public async Task MarkStepCompensatedAsync(Guid theSagaId, string theStepName, CancellationToken theCancellationToken = default)
    {
        var aDocument = await LoadTrackedAsync(theSagaId, theCancellationToken);
        var aStep = aDocument?.Steps.FirstOrDefault(s => s.Name == theStepName);
        if (aStep is null)
        {
            return;
        }

        aStep.Compensated = true;
        aDocument!.UpdatedUtc = DateTime.UtcNow;
        await myContext.SaveChangesAsync(theCancellationToken);
    }

    public async Task SetStatusAsync(Guid theSagaId, SagaStatus theStatus, CancellationToken theCancellationToken = default)
    {
        var aDocument = await LoadTrackedAsync(theSagaId, theCancellationToken);
        if (aDocument is null)
        {
            return;
        }

        aDocument.Status = (int)theStatus;
        aDocument.UpdatedUtc = DateTime.UtcNow;
        await myContext.SaveChangesAsync(theCancellationToken);
    }

    public async Task<int> IncrementRetryAsync(Guid theSagaId, CancellationToken theCancellationToken = default)
    {
        var aDocument = await LoadTrackedAsync(theSagaId, theCancellationToken);
        if (aDocument is null)
        {
            return 0;
        }

        aDocument.RetryCount += 1;
        aDocument.UpdatedUtc = DateTime.UtcNow;
        await myContext.SaveChangesAsync(theCancellationToken);
        return aDocument.RetryCount;
    }

    public async Task<SagaSnapshot?> LoadAsync(Guid theSagaId, CancellationToken theCancellationToken = default)
    {
        var aDocument = await myContext.SagaInstances
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == theSagaId, theCancellationToken);

        return aDocument is null ? null : ToSnapshot(aDocument);
    }

    public async Task<IReadOnlyList<SagaSnapshot>> GetUnfinishedAsync(CancellationToken theCancellationToken = default)
    {
        var aStarted = (int)SagaStatus.Started;
        var aCompensating = (int)SagaStatus.Compensating;
        var aManual = (int)SagaStatus.NeedsManualReview;

        var aDocuments = await myContext.SagaInstances
            .AsNoTracking()
            .Where(s => s.Status == aStarted || s.Status == aCompensating || s.Status == aManual)
            .ToListAsync(theCancellationToken);

        return aDocuments.Select(ToSnapshot).ToList();
    }

    public async Task RemoveAsync(Guid theSagaId, CancellationToken theCancellationToken = default)
    {
        var aDocument = await LoadTrackedAsync(theSagaId, theCancellationToken);
        if (aDocument is null)
        {
            return;
        }

        myContext.SagaInstances.Remove(aDocument);
        await myContext.SaveChangesAsync(theCancellationToken);
    }

    private async Task<SagaInstanceDocument?> LoadTrackedAsync(Guid theSagaId, CancellationToken theCancellationToken)
        => await myContext.SagaInstances.FirstOrDefaultAsync(s => s.Id == theSagaId, theCancellationToken);

    private static SagaStepDocument ToDocument(SagaStepInfo theStep) => new()
    {
        Name = theStep.Name,
        Kind = (int)theStep.Kind,
        CompensationType = theStep.CompensationType,
        CompensationData = theStep.CompensationData,
        Compensated = theStep.Compensated
    };

    private static SagaSnapshot ToSnapshot(SagaInstanceDocument theDocument) => new(
        theDocument.Id,
        theDocument.Name,
        (SagaStatus)theDocument.Status,
        theDocument.Steps
            .Select(s => new SagaStepInfo(s.Name, (SagaStepKind)s.Kind, s.CompensationType, s.CompensationData, s.Compensated))
            .ToList(),
        theDocument.RetryCount);
}
