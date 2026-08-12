using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SellingNewProduct.Infrastructure.Messaging.Abstractions;
using SellingNewProduct.Infrastructure.Messaging.Consumers;
using SellingNewProduct.Infrastructure.Messaging.Contracts;
using SellingNewProduct.Infrastructure.Messaging.Kafka;
using SellingNewProduct.Infrastructure.Messaging.Outbox;
using SellingNewProduct.Infrastructure.Messaging.RabbitMq;
using SellingNewProduct.Infrastructure.Messaging.Resilience;
using SellingNewProduct.Infrastructure.Messaging.Routing;
using SellingNewProduct.Infrastructure.Messaging.Services;
using SellingNewProduct.Infrastructure.Messaging.Workers;

namespace SellingNewProduct.Infrastructure.Messaging;

/// <summary>
/// Composition for the messaging layer. The transactional outbox routes each message AT THE SOURCE by
/// its nature: facts → Kafka (event backbone: fan-out, replay, analytics), commands → RabbitMQ (work
/// queue: ack, retry, dead-letter). There is NO Kafka→RabbitMQ bridge — each broker is used directly
/// for what it is best at. The database projects supply the <see cref="IOutboxStore"/>(s) (SQL owns
/// Order/Payment, MongoDB owns the catalogue) and the <see cref="Routing.OutboxRouter"/> comes from
/// the saga core.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddMessaging(this IServiceCollection theServices, IConfiguration theConfiguration)
    {
        theServices.Configure<KafkaSettings>(theConfiguration.GetSection(KafkaSettings.SectionName));
        theServices.Configure<RabbitMqSettings>(theConfiguration.GetSection(RabbitMqSettings.SectionName));

        theServices.AddSingleton(BuildEventRegistry());
        theServices.AddSingleton(BuildCommandRegistry());

        // Kafka = event backbone (facts). One producer used by the outbox, one consumer host (fan-out).
        theServices.AddSingleton<IEventBus, KafkaEventBus>();
        theServices.AddHostedService<KafkaConsumerHostedService>();

        // RabbitMQ = command/work queue. Shared connection, a raw publisher, one consumer host.
        theServices.AddSingleton<RabbitMqConnectionProvider>();
        theServices.AddSingleton<ICommandPublisher, RabbitMqCommandPublisher>();
        theServices.AddHostedService<RabbitMqConsumerHostedService>();

        // Wake-up signal (Channel) so the write side can trigger an immediate drain instead of waiting
        // for the next poll, plus an in-memory ring buffer (ConcurrentQueue) of recent dispatch results
        // for the diagnostics endpoint. Both singletons: producers and the dispatcher share one instance.
        theServices.AddSingleton<IOutboxSignal, OutboxSignal>();
        theServices.AddSingleton<IOutboxActivityLog, OutboxActivityLog>();
        theServices.AddSingleton<OutboxMetrics>();

        // The relay that drains every outbox and routes each row to Kafka or RabbitMQ by destination.
        theServices.AddHostedService<OutboxDispatcher>();

        // Polly resilience shared by the command workers that call downstream gateways.
        theServices.AddDownstreamResilience();

        RegisterEventHandlers(theServices);
        RegisterCommandWorkers(theServices);
        RegisterSideEffects(theServices, theConfiguration);

        return theServices;
    }

    /// <summary>Maps each integration event (fact) to its Kafka topic and dispatch closure.</summary>
    private static IntegrationEventRegistry BuildEventRegistry()
    {
        var aRegistry = new IntegrationEventRegistry();
        aRegistry.Register<OrderPlacedIntegrationEvent>(MessagingTopics.OrderEvents);
        aRegistry.Register<OrderConfirmedIntegrationEvent>(MessagingTopics.OrderEvents);
        aRegistry.Register<OrderShippedIntegrationEvent>(MessagingTopics.OrderEvents);
        aRegistry.Register<OrderCancelledIntegrationEvent>(MessagingTopics.OrderEvents);
        aRegistry.Register<PaymentCompletedIntegrationEvent>(MessagingTopics.PaymentEvents);
        aRegistry.Register<ProductStockChangedIntegrationEvent>(MessagingTopics.ProductEvents);
        return aRegistry;
    }

    /// <summary>Maps each command (work) to its RabbitMQ queue and handler-dispatch closure.</summary>
    private static CommandRegistry BuildCommandRegistry()
    {
        var aRegistry = new CommandRegistry();
        aRegistry.Register<SendEmailCommand>(MessagingQueues.SendEmail);
        aRegistry.Register<IssueInvoiceCommand>(MessagingQueues.IssueInvoice);
        aRegistry.Register<SendNotificationCommand>(MessagingQueues.SendNotification);
        aRegistry.Register<RestockAlertCommand>(MessagingQueues.RestockAlert);
        return aRegistry;
    }

    /// <summary>Kafka consumers. These read FACTS off the log — no command routing happens here (that
    /// is done at the source by the outbox router). Analytics is an independent reader of the stream.</summary>
    private static void RegisterEventHandlers(IServiceCollection theServices)
    {
        theServices.AddScoped<IIntegrationEventHandler<OrderPlacedIntegrationEvent>, AnalyticsProjectionHandler>();
        theServices.AddScoped<IIntegrationEventHandler<OrderConfirmedIntegrationEvent>, AnalyticsProjectionHandler>();
        theServices.AddScoped<IIntegrationEventHandler<OrderCancelledIntegrationEvent>, AnalyticsProjectionHandler>();
        theServices.AddScoped<IIntegrationEventHandler<PaymentCompletedIntegrationEvent>, AnalyticsProjectionHandler>();
        theServices.AddScoped<IIntegrationEventHandler<ProductStockChangedIntegrationEvent>, AnalyticsProjectionHandler>();
    }

    /// <summary>RabbitMQ workers. Exactly one handler per command.</summary>
    private static void RegisterCommandWorkers(IServiceCollection theServices)
    {
        theServices.AddScoped<ICommandHandler<SendEmailCommand>, EmailCommandWorker>();
        theServices.AddScoped<ICommandHandler<IssueInvoiceCommand>, InvoiceCommandWorker>();
        theServices.AddScoped<ICommandHandler<SendNotificationCommand>, NotificationCommandWorker>();
        theServices.AddScoped<ICommandHandler<RestockAlertCommand>, RestockAlertCommandWorker>();
    }

    /// <summary>
    /// The downstream gateways. When <c>Gateways:UseReal</c> is true the email/invoice ports bind to the
    /// real SMTP + PDF adapters (needing <c>Gateways:Smtp</c>/<c>Gateways:Invoice</c> config); otherwise
    /// they fall back to the logging stand-ins so the app still runs locally without credentials. Either
    /// way the command worker calls them through the Polly circuit breaker — the consumer never changes.
    /// </summary>
    private static void RegisterSideEffects(IServiceCollection theServices, IConfiguration theConfiguration)
    {
        var aUseReal = theConfiguration.GetValue<bool>("Gateways:UseReal");

        if (aUseReal)
        {
            theServices.Configure<SmtpOptions>(theConfiguration.GetSection(SmtpOptions.SectionName));
            theServices.Configure<InvoiceOptions>(theConfiguration.GetSection(InvoiceOptions.SectionName));

            theServices.AddScoped<IEmailSender, SmtpEmailSender>();
            theServices.AddScoped<IInvoiceIssuer, PdfInvoiceIssuer>();
        }
        else
        {
            theServices.AddScoped<IEmailSender, LoggingEmailSender>();
            theServices.AddScoped<IInvoiceIssuer, LoggingInvoiceIssuer>();
        }

        // Notification/restock stay logging stand-ins for now (no real provider chosen).
        theServices.AddScoped<INotificationSender, LoggingNotificationSender>();
        theServices.AddScoped<IRestockAlerter, LoggingRestockAlerter>();
        theServices.AddSingleton<IAnalyticsStore, InMemoryAnalyticsStore>();
    }
}
