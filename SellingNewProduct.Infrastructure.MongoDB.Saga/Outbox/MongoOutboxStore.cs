using Microsoft.EntityFrameworkCore;
using SellingNewProduct.Infrastructure.Messaging.Abstractions;
using SellingNewProduct.Infrastructure.MongoDB.Saga.Persistence;

namespace SellingNewProduct.Infrastructure.MongoDB.Saga.Outbox;

/// <summary>
/// MongoDB-backed <see cref="IOutboxStore"/> for the catalogue's outbox. Registered as an additional
/// <c>IOutboxStore</c>, so the shared <c>OutboxDispatcher</c> drains BOTH this and the SQL outbox and
/// routes each row to its broker. Keeping the store in the database project keeps messaging
/// storage-agnostic — the exact mirror of the SQL side.
/// </summary>
internal sealed class MongoOutboxStore : IOutboxStore
{
    private readonly MongoAppDbContext myContext;

    public MongoOutboxStore(MongoAppDbContext theContext)
    {
        myContext = theContext;
    }

    public async Task<IReadOnlyList<OutboxMessage>> GetUnpublishedAsync(int theBatchSize, CancellationToken theCancellationToken = default)
    {
        var aDocuments = await myContext.OutboxMessages
            .Where(x => x.ProcessedUtc == null)
            .OrderBy(x => x.CreatedUtc)
            .Take(theBatchSize)
            .AsNoTracking()
            .ToListAsync(theCancellationToken);

        return aDocuments
            .Select(x => new OutboxMessage(x.Id, (OutboxDestination)x.Destination, x.Route, x.MessageType, x.Payload, x.PartitionKey))
            .ToList();
    }

    public async Task MarkPublishedAsync(Guid theId, CancellationToken theCancellationToken = default)
    {
        var aDocument = await myContext.OutboxMessages.FirstOrDefaultAsync(x => x.Id == theId, theCancellationToken);
        if (aDocument is null)
        {
            return;
        }

        aDocument.ProcessedUtc = DateTime.UtcNow;
        aDocument.Error = null;
        await myContext.SaveChangesAsync(theCancellationToken);
    }

    public async Task MarkFailedAsync(Guid theId, string theError, CancellationToken theCancellationToken = default)
    {
        var aDocument = await myContext.OutboxMessages.FirstOrDefaultAsync(x => x.Id == theId, theCancellationToken);
        if (aDocument is null)
        {
            return;
        }

        aDocument.RetryCount += 1;
        aDocument.Error = theError.Length > 1000 ? theError[..1000] : theError;
        await myContext.SaveChangesAsync(theCancellationToken);
    }
}
