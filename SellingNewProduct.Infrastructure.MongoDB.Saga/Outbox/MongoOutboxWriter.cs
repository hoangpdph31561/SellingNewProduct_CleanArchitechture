using SellingNewProduct.Domain.Common;
using SellingNewProduct.Infrastructure.Messaging.Outbox;
using SellingNewProduct.Infrastructure.Saga.Core.Outbox;
using SellingNewProduct.Infrastructure.MongoDB.Saga.Models;
using SellingNewProduct.Infrastructure.MongoDB.Saga.Persistence;

namespace SellingNewProduct.Infrastructure.MongoDB.Saga.Outbox;

/// <summary>
/// Stages catalogue outbox documents onto the shared <see cref="MongoAppDbContext"/> WITHOUT saving,
/// so the write repository's own transaction commits the stock change and these rows atomically (the
/// Mongo counterpart of the SQL <c>OutboxWriter</c>). Uses the same shared <see cref="OutboxRouter"/>,
/// so a stock movement routes facts→Kafka / commands→RabbitMQ exactly like an order does.
/// </summary>
internal sealed class MongoOutboxWriter
{
    private readonly MongoAppDbContext myContext;
    private readonly OutboxRouter myRouter;
    private readonly IOutboxSignal mySignal;

    public MongoOutboxWriter(MongoAppDbContext theContext, OutboxRouter theRouter, IOutboxSignal theSignal)
    {
        myContext = theContext;
        myRouter = theRouter;
        mySignal = theSignal;
    }

    /// <summary>Stages outbox rows for every event across the given aggregates; clears their events.</summary>
    public int Stage(IEnumerable<AggregateRoot<Guid>> theAggregates)
    {
        var aStaged = 0;

        foreach (var aAggregate in theAggregates)
        {
            var aEntries = myRouter.Route(aAggregate.DomainEvents);

            foreach (var aEntry in aEntries)
            {
                myContext.OutboxMessages.Add(new OutboxMessageDocument
                {
                    Id = Guid.NewGuid(),
                    Destination = (int)aEntry.Destination,
                    Route = aEntry.Route,
                    MessageType = aEntry.MessageType,
                    Payload = aEntry.Payload,
                    PartitionKey = aEntry.PartitionKey,
                    CreatedUtc = DateTime.UtcNow
                });
                aStaged++;
            }

            aAggregate.ClearDomainEvents();
        }

        // Wake the dispatcher for a near-instant drain after the caller's transaction commits (the poll
        // remains the safety net). Coalesced, best-effort — see the SQL OutboxWriter for the rationale.
        if (aStaged > 0)
        {
            mySignal.Notify();
        }

        return aStaged;
    }
}
