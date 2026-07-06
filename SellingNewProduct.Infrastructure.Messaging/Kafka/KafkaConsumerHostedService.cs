using System.Text;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SellingNewProduct.Infrastructure.Messaging.Routing;

namespace SellingNewProduct.Infrastructure.Messaging.Kafka;

/// <summary>
/// Subscribes to every topic in the <see cref="IntegrationEventRegistry"/> and drives the consume
/// loop. For each message it reads the <c>eventType</c> header, opens a DI scope, and lets the
/// registry deserialize and fan the event out to all its handlers. Offsets are committed only AFTER
/// the handlers succeed (manual commit) — at-least-once delivery, so handlers must be idempotent. If
/// the broker is unreachable the loop backs off and retries instead of crashing the host.
/// </summary>
public sealed class KafkaConsumerHostedService : BackgroundService
{
    private readonly IServiceScopeFactory myScopeFactory;
    private readonly IntegrationEventRegistry myRegistry;
    private readonly KafkaSettings mySettings;
    private readonly ILogger<KafkaConsumerHostedService> myLogger;

    public KafkaConsumerHostedService(
        IServiceScopeFactory theScopeFactory,
        IntegrationEventRegistry theRegistry,
        IOptions<KafkaSettings> theSettings,
        ILogger<KafkaConsumerHostedService> theLogger)
    {
        myScopeFactory = theScopeFactory;
        myRegistry = theRegistry;
        mySettings = theSettings.Value;
        myLogger = theLogger;
    }

    protected override Task ExecuteAsync(CancellationToken theStoppingToken)
    {
        // The Confluent consumer is blocking, so run the loop on a dedicated background thread.
        return Task.Run(() => RunConsumeLoop(theStoppingToken), theStoppingToken);
    }

    private async Task RunConsumeLoop(CancellationToken theStoppingToken)
    {
        var aConfig = new ConsumerConfig
        {
            BootstrapServers = mySettings.BootstrapServers,
            GroupId = mySettings.ConsumerGroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        while (!theStoppingToken.IsCancellationRequested)
        {
            try
            {
                using var aConsumer = new ConsumerBuilder<string, string>(aConfig).Build(); // EnableAutoCommit=false
                aConsumer.Subscribe(myRegistry.Topics);
                myLogger.LogInformation("Kafka ◀ consuming {Topics} as group '{Group}'.",
                    string.Join(", ", myRegistry.Topics), mySettings.ConsumerGroupId);

                while (!theStoppingToken.IsCancellationRequested)
                {
                    var aResult = aConsumer.Consume(theStoppingToken);
                    if (aResult?.Message is null)
                    {
                        continue;
                    }

                    await HandleMessageAsync(aResult, theStoppingToken);
                    aConsumer.Commit(aResult); // Ack only after successful handling.
                }
            }
            catch (OperationCanceledException) when (theStoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception aException)
            {
                // Broker down / rebalance / handler blew up: back off and rebuild the consumer.
                myLogger.LogWarning(aException, "Kafka consumer error; reconnecting in 5s.");
                await Task.Delay(TimeSpan.FromSeconds(5), theStoppingToken);
            }
        }
    }

    private async Task HandleMessageAsync(ConsumeResult<string, string> theResult, CancellationToken theCancellationToken)
    {
        var aEventType = ReadEventType(theResult.Message.Headers);
        if (aEventType is null)
        {
            myLogger.LogWarning("Kafka message on {Topic} has no {Header} header; skipping.",
                theResult.Topic, KafkaEventBus.EventTypeHeader);
            return;
        }

        var aRegistration = myRegistry.Resolve(aEventType);
        if (aRegistration is null)
        {
            // Unknown to this deployment — another service may own it. Skip, do not fail.
            myLogger.LogDebug("No handler for event '{EventType}'; skipping.", aEventType);
            return;
        }

        await using var aScope = myScopeFactory.CreateAsyncScope();
        await aRegistration.Dispatch(aScope.ServiceProvider, theResult.Message.Value, theCancellationToken);
    }

    private static string? ReadEventType(Headers theHeaders)
    {
        if (theHeaders.TryGetLastBytes(KafkaEventBus.EventTypeHeader, out var aBytes))
        {
            return Encoding.UTF8.GetString(aBytes);
        }

        return null;
    }
}
