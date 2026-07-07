using System.Text;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using SellingNewProduct.Infrastructure.Messaging.Abstractions;
using SellingNewProduct.Infrastructure.Messaging.Contracts;

namespace SellingNewProduct.Infrastructure.Messaging.RabbitMq;

/// <summary>
/// RabbitMQ adapter for <see cref="ICommandPublisher"/>. Publishes a persistent, JSON command to the
/// direct exchange with the given queue as the routing key. Persistent + a durable queue means the
/// work survives a broker restart — the reliability guarantee for "send the email no matter what".
/// The outbox dispatcher calls this directly for every command row (no bridge from Kafka).
/// </summary>
public sealed class RabbitMqCommandPublisher : ICommandPublisher
{
    private readonly RabbitMqConnectionProvider myConnectionProvider;
    private readonly ILogger<RabbitMqCommandPublisher> myLogger;

    public RabbitMqCommandPublisher(RabbitMqConnectionProvider theConnectionProvider, ILogger<RabbitMqCommandPublisher> theLogger)
    {
        myConnectionProvider = theConnectionProvider;
        myLogger = theLogger;
    }
    //RabbitMqCommandPublisher.PublishAsync là bên GỬI của RabbitMQ — nó đặt một command vào queue. Đối xứng với RabbitMqConsumerHostedService
    public async Task PublishAsync(string theQueue, string theCommandType, string thePayloadJson, CancellationToken theCancellationToken = default)
    {
        var aBody = Encoding.UTF8.GetBytes(thePayloadJson);

        var aConnection = await myConnectionProvider.GetConnectionAsync(theCancellationToken);
        await using var aChannel = await aConnection.CreateChannelAsync(cancellationToken: theCancellationToken);

        // Idempotent topology declaration so publishing works even before the consumer host booted.
        await aChannel.ExchangeDeclareAsync(
            MessagingQueues.Exchange, ExchangeType.Direct, durable: true, autoDelete: false, cancellationToken: theCancellationToken);

        var aProperties = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
            Type = theCommandType
        };

        await aChannel.BasicPublishAsync(
            MessagingQueues.Exchange, routingKey: theQueue, mandatory: false, basicProperties: aProperties, body: aBody,
            cancellationToken: theCancellationToken);

        myLogger.LogInformation("RabbitMQ ▶ queued {Command} to '{Queue}'.", theCommandType, theQueue);
    }
}
