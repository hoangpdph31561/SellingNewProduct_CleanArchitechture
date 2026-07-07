using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SellingNewProduct.Infrastructure.Messaging.Contracts;
using SellingNewProduct.Infrastructure.Messaging.Routing;

namespace SellingNewProduct.Infrastructure.Messaging.RabbitMq;

/// <summary>
/// Declares the command topology (a direct exchange, one durable queue per command, each wired to a
/// dead-letter exchange) and runs one competing consumer per queue. A message is retried in-process up
/// to <see cref="RabbitMqSettings.MaxRetries"/> times; if it still fails it is rejected without requeue
/// and RabbitMQ routes it to the queue's dead-letter queue for inspection. Ack-after-success gives
/// each command at-least-once, exactly-one-worker semantics — the delivery guarantee a work queue is
/// built for, and the reason commands go here rather than on the Kafka log.
/// </summary>
public sealed class RabbitMqConsumerHostedService : BackgroundService
{
    private readonly IServiceScopeFactory myScopeFactory;
    private readonly RabbitMqConnectionProvider myConnectionProvider;
    private readonly CommandRegistry myRegistry;
    private readonly RabbitMqSettings mySettings;
    private readonly ILogger<RabbitMqConsumerHostedService> myLogger;

    private readonly List<IChannel> myChannels = new();

    public RabbitMqConsumerHostedService(
        IServiceScopeFactory theScopeFactory,
        RabbitMqConnectionProvider theConnectionProvider,
        CommandRegistry theRegistry,
        IOptions<RabbitMqSettings> theSettings,
        ILogger<RabbitMqConsumerHostedService> theLogger)
    {
        myScopeFactory = theScopeFactory;
        myConnectionProvider = theConnectionProvider;
        myRegistry = theRegistry;
        mySettings = theSettings.Value;
        myLogger = theLogger;
    }
    //bên NHẬN. Vai trò của nó không phải xử lý message, mà là "người giám sát" (supervisor): dựng consumer → ngồi canh → hỏng/mất kết nối thì dựng lại → app tắt thì dọn dẹp.
    protected override async Task ExecuteAsync(CancellationToken theStoppingToken)
    {
        while (!theStoppingToken.IsCancellationRequested)
        {
            try
            {
                await StartConsumersAsync(theStoppingToken);

                // Consumers run on the connection's callback threads; idle here until shutdown or a
                // dropped channel, then rebuild.
                while (!theStoppingToken.IsCancellationRequested && myChannels.All(c => c.IsOpen))
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), theStoppingToken);
                }
            }
            catch (OperationCanceledException) when (theStoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception aException)
            {
                myLogger.LogWarning(aException, "RabbitMQ consumer setup failed; retrying in 5s.");
                await Task.Delay(TimeSpan.FromSeconds(5), theStoppingToken);
            }

            await CloseChannelsAsync();
        }

        await CloseChannelsAsync();
    }

    private async Task StartConsumersAsync(CancellationToken theCancellationToken)
    {
        // (1) xoá sạch channel cũ trước khi dựng mới
        await CloseChannelsAsync();

        // (2) lấy connection 
        var aConnection = await myConnectionProvider.GetConnectionAsync(theCancellationToken);

        // One channel just to declare the shared exchanges.
        await using (var aSetup = await aConnection.CreateChannelAsync(cancellationToken: theCancellationToken))
        {
            //4 exchange chính
            await aSetup.ExchangeDeclareAsync(MessagingQueues.Exchange, ExchangeType.Direct, durable: true, cancellationToken: theCancellationToken);
            //5 DLX
            await aSetup.ExchangeDeclareAsync(MessagingQueues.DeadLetterExchange, ExchangeType.Direct, durable: true, cancellationToken: theCancellationToken);
        }
        //MỖI QUEUE — dựng topology + treo consumer
        foreach (var aQueue in myRegistry.Queues)
        {
            var aChannel = await aConnection.CreateChannelAsync(cancellationToken: theCancellationToken);
            //9 khai queue chính + DLQ + 2 binding
            await DeclareQueueTopologyAsync(aChannel, aQueue, theCancellationToken);

            // Prefetch 1 keeps in-flight work at one per queue, so the retry loop below can own the
            // channel without concurrent deliveries racing on the same ack.
            //10 mỗi lúc chỉ nhận 1 message
            await aChannel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, cancellationToken: theCancellationToken);

            var aConsumer = new AsyncEventingBasicConsumer(aChannel);
            aConsumer.ReceivedAsync += (theSender, theArgs) => OnMessageAsync(aChannel, aQueue, theArgs, theCancellationToken);

            await aChannel.BasicConsumeAsync(aQueue, autoAck: false, aConsumer, cancellationToken: theCancellationToken);
            myChannels.Add(aChannel);
        }

        myLogger.LogInformation("RabbitMQ ◀ consuming {Queues}.", string.Join(", ", myRegistry.Queues));
    }

    private static async Task DeclareQueueTopologyAsync(IChannel theChannel, string theQueue, CancellationToken theCancellationToken)
    {
        var aDeadLetterQueue = theQueue + ".dlq";

        // Dead-letter queue: where a command lands after it exhausts its retries.
        await theChannel.QueueDeclareAsync(aDeadLetterQueue, durable: true, exclusive: false, autoDelete: false, cancellationToken: theCancellationToken);
        await theChannel.QueueBindAsync(aDeadLetterQueue, MessagingQueues.DeadLetterExchange, routingKey: theQueue, cancellationToken: theCancellationToken);

        // Main work queue, dead-lettering to the DLX on reject.
        var aArgs = new Dictionary<string, object?>
        {
            ["x-dead-letter-exchange"] = MessagingQueues.DeadLetterExchange,
            ["x-dead-letter-routing-key"] = theQueue
        };
        await theChannel.QueueDeclareAsync(theQueue, durable: true, exclusive: false, autoDelete: false, arguments: aArgs, cancellationToken: theCancellationToken);
        await theChannel.QueueBindAsync(theQueue, MessagingQueues.Exchange, routingKey: theQueue, cancellationToken: theCancellationToken);
    }

    private async Task OnMessageAsync(IChannel theChannel, string theQueue, BasicDeliverEventArgs theArgs, CancellationToken theCancellationToken)
    {
        // (1) tra closure của queue này
        var aRegistration = myRegistry.ByQueue(theQueue);
        if (aRegistration is null)
        {
            await theChannel.BasicAckAsync(theArgs.DeliveryTag, multiple: false, theCancellationToken);
            return;
        }

        var aPayload = Encoding.UTF8.GetString(theArgs.Body.Span);

        for (var anAttempt = 1; anAttempt <= mySettings.MaxRetries; anAttempt++)
        {
            try
            {
                // (5) scope MỚI mỗi lần thử
                await using var aScope = myScopeFactory.CreateAsyncScope();
                await aRegistration.Dispatch(aScope.ServiceProvider, aPayload, theCancellationToken);
                // (7) OK → ACK, xong báo RabbitMQ "xử lý xong, xoá message khỏi queue"
                await theChannel.BasicAckAsync(theArgs.DeliveryTag, multiple: false, theCancellationToken);
                return;
            }
            catch (Exception aException) when (anAttempt < mySettings.MaxRetries)
            {
                myLogger.LogWarning(aException,
                    "RabbitMQ: '{Queue}' handler failed (attempt {Attempt}/{Max}); retrying.", theQueue, anAttempt, mySettings.MaxRetries);
                await Task.Delay(TimeSpan.FromMilliseconds(300 * anAttempt), theCancellationToken);
            }
            catch (Exception aException)
            {
                myLogger.LogError(aException,
                    "RabbitMQ: '{Queue}' handler failed permanently; dead-lettering the message.", theQueue);
                await theChannel.BasicNackAsync(theArgs.DeliveryTag, multiple: false, requeue: false, theCancellationToken);
                return;
            }
        }
    }

    private async Task CloseChannelsAsync()
    {
        foreach (var aChannel in myChannels)
        {
            try
            {
                await aChannel.CloseAsync();
                await aChannel.DisposeAsync();
            }
            catch
            {
                // Best-effort cleanup.
            }
        }

        myChannels.Clear();
    }
}
