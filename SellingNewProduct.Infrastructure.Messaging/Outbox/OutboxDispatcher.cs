using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SellingNewProduct.Infrastructure.Messaging.Abstractions;

namespace SellingNewProduct.Infrastructure.Messaging.Outbox;

/// <summary>
/// The relay that turns the transactional outbox into published Kafka events. It polls the store for
/// rows the business transaction committed but has not yet published, sends each to the event bus,
/// and marks it done. This is what makes "change the database AND publish an event" reliable without a
/// distributed transaction: the write and the outbox row are one local commit; publishing happens
/// afterwards, at-least-once (so consumers must be idempotent). If the broker is down the rows simply
/// stay unpublished and are retried on the next tick — nothing is lost.
/// </summary>
public sealed class OutboxDispatcher : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    private const int BatchSize = 50;

    private readonly IServiceScopeFactory myScopeFactory;
    private readonly IEventBus myEventBus;
    private readonly ICommandPublisher myCommandPublisher;
    private readonly ILogger<OutboxDispatcher> myLogger;

    public OutboxDispatcher(
        IServiceScopeFactory theScopeFactory,
        IEventBus theEventBus,
        ICommandPublisher theCommandPublisher,
        ILogger<OutboxDispatcher> theLogger)
    {
        myScopeFactory = theScopeFactory;
        myEventBus = theEventBus;
        myCommandPublisher = theCommandPublisher;
        myLogger = theLogger;
    }

    protected override async Task ExecuteAsync(CancellationToken theStoppingToken)
    {
        while (!theStoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchBatchAsync(theStoppingToken);
            }
            catch (OperationCanceledException) when (theStoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception aException)
            {
                // Never let a bad tick kill the loop; the same rows are retried next time.
                myLogger.LogError(aException, "Outbox dispatch tick failed; will retry.");
            }

            await Task.Delay(PollInterval, theStoppingToken);
        }
    }

    private async Task DispatchBatchAsync(CancellationToken theCancellationToken)
    {
        await using var aScope = myScopeFactory.CreateAsyncScope();

        // There may be more than one outbox (SQL owns Order/Payment, MongoDB owns the catalogue), so
        // drain every registered store — each is its own transactional outbox.
        var aStores = aScope.ServiceProvider.GetServices<IOutboxStore>();

        foreach (var aStore in aStores)
        {
            await DrainStoreAsync(aStore, theCancellationToken);
        }
    }

    private async Task DrainStoreAsync(IOutboxStore theStore, CancellationToken theCancellationToken)
    {
        var aMessages = await theStore.GetUnpublishedAsync(BatchSize, theCancellationToken);

        foreach (var aMessage in aMessages)
        {
            try
            {
                await PublishAsync(aMessage, theCancellationToken);
                await theStore.MarkPublishedAsync(aMessage.Id, theCancellationToken);
            }
            catch (Exception aException)
            {
                // Leave it unpublished (record the reason) so the next tick retries it.
                myLogger.LogWarning(aException, "Outbox: failed to publish {Id} ({Type}).", aMessage.Id, aMessage.MessageType);
                await theStore.MarkFailedAsync(aMessage.Id, aException.Message, theCancellationToken);
            }
        }
    }

    /// <summary>Route by nature, at the source: a fact goes to Kafka, a command goes to RabbitMQ.
    /// This is where "each broker handles what it is best at" is enforced — no funnel, no bridge.</summary>
    private Task PublishAsync(OutboxMessage theMessage, CancellationToken theCancellationToken) => theMessage.Destination switch
    {
        OutboxDestination.Kafka =>
            myEventBus.PublishAsync(theMessage.Route, theMessage.PartitionKey, theMessage.MessageType, theMessage.Payload, theCancellationToken),

        OutboxDestination.RabbitMq =>
            myCommandPublisher.PublishAsync(theMessage.Route, theMessage.MessageType, theMessage.Payload, theCancellationToken),

        _ => throw new InvalidOperationException($"Unknown outbox destination '{theMessage.Destination}'.")
    };
}
