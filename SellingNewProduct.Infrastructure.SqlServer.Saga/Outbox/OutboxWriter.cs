using SellingNewProduct.Domain.Common;
using SellingNewProduct.Infrastructure.Messaging.Outbox;
using SellingNewProduct.Infrastructure.Saga.Core.Outbox;
using SellingNewProduct.Infrastructure.SqlServer.Saga.Models;
using SellingNewProduct.Infrastructure.SqlServer.Saga.Persistence;

namespace SellingNewProduct.Infrastructure.SqlServer.Saga.Outbox;

/// <summary>
/// Stages an aggregate's outbox rows onto the shared <see cref="AppDbContext"/> WITHOUT saving — the
/// calling repository's own <c>SaveChanges</c> commits the business change and these rows in one
/// transaction. The routing decision (fact→Kafka / command→RabbitMQ) is made by the shared
/// <see cref="OutboxRouter"/>; this writer only turns the resulting entries into SQL rows. After
/// staging it clears the aggregate's events so they cannot be enqueued twice.
/// </summary>
internal sealed class OutboxWriter
{
    private readonly AppDbContext myAppDbContext;
    private readonly OutboxRouter myRouter;
    private readonly IOutboxSignal mySignal;

    public OutboxWriter(AppDbContext theAppDbContext, OutboxRouter theRouter, IOutboxSignal theSignal)
    {
        myAppDbContext = theAppDbContext;
        myRouter = theRouter;
        mySignal = theSignal;
    }

    /// <summary>Stages every message the aggregate's events produce; returns how many rows were added.</summary>
    public int Stage(AggregateRoot<Guid> theAggregate)
    {
        var aEntries = myRouter.Route(theAggregate.DomainEvents);

        foreach (var aEntry in aEntries)
        {
            myAppDbContext.OutboxMessages.Add(new OutboxMessageRecord
            {
                Id = Guid.NewGuid(),
                Destination = (int)aEntry.Destination,
                Route = aEntry.Route,
                MessageType = aEntry.MessageType,
                Payload = aEntry.Payload,
                PartitionKey = aEntry.PartitionKey,
                CreatedUtc = DateTime.UtcNow
            });
        }

        theAggregate.ClearDomainEvents();

        // Ring the dispatcher's bell so it drains right after the caller commits, instead of waiting
        // for the next poll. Best-effort: if the caller's SaveChanges later rolls back, the wake finds
        // nothing and the poll still covers any row that slipped through. Coalesced by the signal.
        if (aEntries.Count > 0)
        {
            mySignal.Notify();
        }

        return aEntries.Count;
    }
}
