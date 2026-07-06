using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SellingNewProduct.Infrastructure.Messaging.Abstractions;
using SellingNewProduct.Infrastructure.Messaging.Contracts;

namespace SellingNewProduct.Infrastructure.Messaging.Routing;

/// <summary>Shared JSON settings so producers and consumers agree on the wire format.</summary>
public static class MessagingJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}

/// <summary>How an integration-event type maps to the wire: its discriminator, its Kafka topic, and
/// the code that deserializes a payload and fans it out to every registered handler.</summary>
public sealed record EventRegistration(
    string EventType,
    string Topic,
    Type ClrType,
    Func<IServiceProvider, string, CancellationToken, Task> Dispatch);

/// <summary>
/// The single source of truth that ties integration-event CLR types to their topic + discriminator
/// name, and knows how to dispatch an incoming payload to the right handlers. Built once at startup
/// (singleton). It removes reflection from the hot path: each <see cref="Register{TEvent}"/> captures
/// a strongly-typed deserialize-and-dispatch closure.
/// </summary>
public sealed class IntegrationEventRegistry
{
    private readonly Dictionary<string, EventRegistration> myByName = new(StringComparer.Ordinal);
    private readonly Dictionary<Type, EventRegistration> myByType = new();

    public void Register<TEvent>(string theTopic) where TEvent : IntegrationEvent
    {
        var aName = typeof(TEvent).Name;
        var aRegistration = new EventRegistration(
            aName,
            theTopic,
            typeof(TEvent),
            async (theProvider, thePayload, theCancellationToken) =>
            {
                var anEvent = JsonSerializer.Deserialize<TEvent>(thePayload, MessagingJson.Options)
                              ?? throw new InvalidOperationException($"Could not deserialize event '{aName}'.");

                // Every handler subscribed to this event type runs — this is the fan-out that makes
                // each handler behave like an independent (future) microservice consumer.
                foreach (var aHandler in theProvider.GetServices<IIntegrationEventHandler<TEvent>>())
                {
                    await aHandler.HandleAsync(anEvent, theCancellationToken);
                }
            });

        myByName[aName] = aRegistration;
        myByType[typeof(TEvent)] = aRegistration;
    }

    /// <summary>Topic + discriminator for an event instance — used by the outbox writer.</summary>
    public EventRegistration Describe(Type theEventType) =>
        myByType.TryGetValue(theEventType, out var aRegistration)
            ? aRegistration
            : throw new InvalidOperationException($"Integration event '{theEventType.Name}' is not registered.");

    public EventRegistration? Resolve(string theEventType) =>
        myByName.TryGetValue(theEventType, out var aRegistration) ? aRegistration : null;

    /// <summary>The distinct topics a Kafka consumer must subscribe to.</summary>
    public IReadOnlyList<string> Topics => myByName.Values.Select(r => r.Topic).Distinct().ToList();
}

/// <summary>How a command type maps to its RabbitMQ queue and the code that runs its single handler.</summary>
public sealed record CommandRegistration(
    string Queue,
    Type ClrType,
    Func<IServiceProvider, string, CancellationToken, Task> Dispatch);

/// <summary>The command counterpart of <see cref="IntegrationEventRegistry"/>: type ↔ queue, plus the
/// deserialize-and-run closure. A command has exactly one handler (one worker does the work).</summary>
public sealed class CommandRegistry
{
    private readonly Dictionary<string, CommandRegistration> myByQueue = new(StringComparer.Ordinal);
    private readonly Dictionary<Type, string> myQueueByType = new();

    public void Register<TCommand>(string theQueue) where TCommand : MessagingCommand
    {
        var aRegistration = new CommandRegistration(
            theQueue,
            typeof(TCommand),
            async (theProvider, thePayload, theCancellationToken) =>
            {
                var aCommand = JsonSerializer.Deserialize<TCommand>(thePayload, MessagingJson.Options)
                               ?? throw new InvalidOperationException($"Could not deserialize command for '{theQueue}'.");

                var aHandler = theProvider.GetRequiredService<ICommandHandler<TCommand>>();
                await aHandler.HandleAsync(aCommand, theCancellationToken);
            });

        myByQueue[theQueue] = aRegistration;
        myQueueByType[typeof(TCommand)] = aRegistration.Queue;
    }

    public string QueueFor(Type theCommandType) =>
        myQueueByType.TryGetValue(theCommandType, out var aQueue)
            ? aQueue
            : throw new InvalidOperationException($"Command '{theCommandType.Name}' is not registered.");

    public CommandRegistration? ByQueue(string theQueue) =>
        myByQueue.TryGetValue(theQueue, out var aRegistration) ? aRegistration : null;

    public IReadOnlyList<string> Queues => myByQueue.Keys.ToList();
}
