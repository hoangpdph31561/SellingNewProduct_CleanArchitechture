using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SellingNewProduct.Domain.Interfaces.Outbound;
using SellingNewProduct.Infrastructure.Saga.Core.Persistence;
using SellingNewProduct.Infrastructure.Saga.Core.Recovery;
using SellingNewProduct.Infrastructure.Saga.Core.Saga;

namespace SellingNewProduct.Infrastructure.Saga.Core;

/// <summary>
/// Composition for the saga kernel. Registers the per-request <see cref="SagaContext"/> and wires
/// the domain <see cref="IUnitOfWork"/> port to the saga implementation. The concrete
/// <see cref="ISagaParticipant"/>s and the durable <see cref="ISagaLog"/> are contributed by the
/// per-database infrastructure projects (SQL + Mongo). Call this first, then the two database
/// registrations.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddSagaCore(this IServiceCollection theServices)
    {
        theServices.AddScoped<SagaContext>();
        theServices.AddScoped<IUnitOfWork, SagaUnitOfWork>();

        // Default no-op log; replaced when a database project registers a durable ISagaLog.
        theServices.TryAddScoped<ISagaLog, NullSagaLog>();

        // Startup reconciliation for sagas interrupted between the two commits. The stores it needs
        // (ISagaEffectStore, ISagaCommitStore) are registered by the Mongo/SQL saga projects.
        theServices.AddHostedService<SagaRecoveryService>();

        return theServices;
    }
}
