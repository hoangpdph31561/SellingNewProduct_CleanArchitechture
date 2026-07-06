using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SellingNewProduct.Domain.Interfaces.Outbound;
using SellingNewProduct.Infrastructure.Saga.Core.Persistence;
using SellingNewProduct.Infrastructure.Saga.Core.Recovery;
using SellingNewProduct.Infrastructure.Saga.Core.Saga;

namespace SellingNewProduct.Infrastructure.Saga.Core;

/// <summary>
/// Composition for the saga orchestration kernel. Registers the per-request <see cref="SagaContext"/>,
/// wires the domain <see cref="IUnitOfWork"/> port to the saga implementation, and sets up the
/// compensation registry + compensator + startup recovery. The durable <see cref="ISagaStore"/> and
/// the concrete <see cref="ISagaCompensationHandler"/>s are contributed by the per-database
/// infrastructure projects (MongoDB owns the store here). Call this first, then the two database
/// registrations.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddSagaCore(this IServiceCollection theServices)
    {
        theServices.AddScoped<SagaContext>();
        theServices.AddScoped<IUnitOfWork, SagaUnitOfWork>();

        // The source-of-truth routing policy (domain event → outbox entries). Both the SQL and the
        // MongoDB outbox writers use it, so both databases route facts→Kafka / commands→RabbitMQ the
        // same way. Depends only on the messaging registries (singletons).
        theServices.AddSingleton<Outbox.OutboxRouter>();

        // Undo machinery: registry of compensation handlers + the shared retrying compensator.
        theServices.AddScoped<ISagaCompensationRegistry, SagaCompensationRegistry>();
        theServices.AddScoped<SagaCompensator>();

        // Startup reconciliation for sagas interrupted mid-flight. The ISagaStore it needs is
        // registered by the MongoDB saga project.
        theServices.AddHostedService<SagaRecoveryService>();

        return theServices;
    }

    /// <summary>
    /// Auto-registers every <see cref="ISagaCompensationHandler"/> found in the given assemblies as a
    /// scoped service, so adding a new compensation step is just dropping a new handler class into the
    /// project — no extra <c>AddScoped</c> line. The <see cref="SagaCompensationRegistry"/> then picks
    /// it up automatically via its injected <c>IEnumerable&lt;ISagaCompensationHandler&gt;</c>.
    /// </summary>
    public static IServiceCollection AddSagaCompensationHandlers(this IServiceCollection theServices, params Assembly[] theAssemblies)
    {
        var aHandlerType = typeof(ISagaCompensationHandler);

        var aImplementations = theAssemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t is { IsAbstract: false, IsInterface: false } && aHandlerType.IsAssignableFrom(t));

        foreach (var aImplementation in aImplementations)
        {
            // TryAddEnumerable keeps the set duplicate-free if an assembly is scanned more than once.
            theServices.TryAddEnumerable(ServiceDescriptor.Scoped(aHandlerType, aImplementation));
        }

        return theServices;
    }
}
