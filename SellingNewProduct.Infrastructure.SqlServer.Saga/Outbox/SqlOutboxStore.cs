using Microsoft.EntityFrameworkCore;
using SellingNewProduct.Infrastructure.Messaging.Abstractions;
using SellingNewProduct.Infrastructure.SqlServer.Saga.Persistence;

namespace SellingNewProduct.Infrastructure.SqlServer.Saga.Outbox;

/// <summary>
/// SQL-backed <see cref="IOutboxStore"/> the <c>OutboxDispatcher</c> polls. It reads the oldest
/// unpublished rows, and stamps each one published (or records the failure) after the event bus has
/// taken it. Keeping this in the database project is what lets the messaging layer stay storage-
/// agnostic.
/// </summary>
internal sealed class SqlOutboxStore : IOutboxStore
{
    private readonly AppDbContext myAppDbContext;

    public SqlOutboxStore(AppDbContext theAppDbContext)
    {
        myAppDbContext = theAppDbContext;
    }

    public async Task<IReadOnlyList<OutboxMessage>> GetUnpublishedAsync(int theBatchSize, CancellationToken theCancellationToken = default)
    {
        var aRecords = await myAppDbContext.OutboxMessages
            .Where(x => x.ProcessedUtc == null)
            .OrderBy(x => x.CreatedUtc)
            .Take(theBatchSize)
            .AsNoTracking()
            .ToListAsync(theCancellationToken);

        return aRecords
            .Select(x => new OutboxMessage(x.Id, (OutboxDestination)x.Destination, x.Route, x.MessageType, x.Payload, x.PartitionKey))
            .ToList();
    }

    public async Task MarkPublishedAsync(Guid theId, CancellationToken theCancellationToken = default)
    {
        var aRecord = await myAppDbContext.OutboxMessages.FirstOrDefaultAsync(x => x.Id == theId, theCancellationToken);
        if (aRecord is null)
        {
            return;
        }

        aRecord.ProcessedUtc = DateTime.UtcNow;
        aRecord.Error = null;
        await myAppDbContext.SaveChangesAsync(theCancellationToken);
    }

    public async Task MarkFailedAsync(Guid theId, string theError, CancellationToken theCancellationToken = default)
    {
        var aRecord = await myAppDbContext.OutboxMessages.FirstOrDefaultAsync(x => x.Id == theId, theCancellationToken);
        if (aRecord is null)
        {
            return;
        }

        aRecord.RetryCount += 1;
        aRecord.Error = theError.Length > 1000 ? theError[..1000] : theError;
        await myAppDbContext.SaveChangesAsync(theCancellationToken);
    }
}
