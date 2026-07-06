using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SellingNewProduct.Infrastructure.Messaging.Abstractions;
using SellingNewProduct.Infrastructure.Messaging.Consumers;
using SellingNewProduct.Infrastructure.Messaging.Contracts;
using SellingNewProduct.Infrastructure.Messaging.Kafka;
using SellingNewProduct.Infrastructure.Messaging.Outbox;
using SellingNewProduct.Infrastructure.Messaging.RabbitMq;
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

        // The relay that drains every outbox and routes each row to Kafka or RabbitMQ by destination.
        theServices.AddHostedService<OutboxDispatcher>();

        RegisterEventHandlers(theServices);
        RegisterCommandWorkers(theServices);
        RegisterSideEffects(theServices);

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

    /// <summary>The faked downstream gateways — swap for real adapters without touching consumers.</summary>
    private static void RegisterSideEffects(IServiceCollection theServices)
    {
        theServices.AddScoped<IEmailSender, LoggingEmailSender>();
        theServices.AddScoped<IInvoiceIssuer, LoggingInvoiceIssuer>();
        theServices.AddScoped<INotificationSender, LoggingNotificationSender>();
        theServices.AddScoped<IRestockAlerter, LoggingRestockAlerter>();
        theServices.AddSingleton<IAnalyticsStore, InMemoryAnalyticsStore>();
    }
}
