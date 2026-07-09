using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SellingNewProduct.Domain.Interfaces.Outbound;
using SellingNewProduct.Infrastructure.Messaging.Abstractions;
using SellingNewProduct.Infrastructure.Saga.Core.CrossDb;
using SellingNewProduct.Infrastructure.Saga.Core.Resilience;
using SellingNewProduct.Infrastructure.SqlServer.Saga.Outbox;
using SellingNewProduct.Infrastructure.SqlServer.Saga.Persistence;
using SellingNewProduct.Infrastructure.SqlServer.Saga.Repositories.Read;
using SellingNewProduct.Infrastructure.SqlServer.Saga.Repositories.Write;
using SellingNewProduct.Infrastructure.SqlServer.Saga.Saga;

namespace SellingNewProduct.Infrastructure.SqlServer.Saga;

/// <summary>
/// Composition for the SQL side of the saga (hybrid) provider. Owns the Order/Payment aggregates,
/// which it commits immediately as the saga's pivot step — there is no SQL saga bookkeeping anymore
/// (the single ledger lives in MongoDB). The cross-database read repositories depend on the MongoDB
/// read ports, registered by the MongoDB saga project — register both alongside <c>AddSagaCore</c>
/// in the composition root.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddSqlServerSagaInfrastructure(this IServiceCollection theServices, IConfiguration theConfiguration)
    {
        // Prefer a dedicated database for the saga provider so it does not collide with the
        // standalone SqlServer provider's schema; fall back to the shared 'SqlServer' string.
        var aConnectionString = theConfiguration.GetConnectionString("SqlServerSaga")
            ?? theConfiguration.GetConnectionString("SqlServer")
            ?? throw new InvalidOperationException("Connection string 'SqlServerSaga'/'SqlServer' was not found.");

        // Business context (Orders/Payments). The saga ledger is NOT here — it lives in MongoDB.
        theServices.AddDbContext<AppDbContext>(o => o.UseSqlServer(aConnectionString));

        // Write side (SQL aggregates).
        theServices.AddScoped<IOrderWriteRepository, SqlOrderWriteRepository>();
        theServices.AddScoped<IPaymentWriteRepository, SqlPaymentWriteRepository>();

        // Read side (SQL aggregation + in-memory enrichment from MongoDB read ports).
        theServices.AddScoped<IOrderReadRepository, SqlOrderReadRepository>();
        theServices.AddScoped<IPaymentReadRepository, SqlPaymentReadRepository>();
        theServices.AddScoped<IReportReadRepository, SqlReportReadRepository>();

        // SQL-backed order statistics consumed by the MongoDB people read models (cycle-free leaf),
        // wrapped in the cross-store circuit breaker: if SQL is unhealthy the breaker trips and the
        // people read models fall back to zero/empty stats instead of failing (decorator pattern).
        theServices.AddScoped<SqlCrossDbOrderStats>();
        theServices.AddScoped<ICrossDbOrderStats>(sp => new ResilientCrossDbOrderStats(
            sp.GetRequiredService<SqlCrossDbOrderStats>(),
            sp.GetRequiredService<CrossStoreToSqlPipeline>(),
            sp.GetRequiredService<ILogger<ResilientCrossDbOrderStats>>()));

        // Transactional outbox: the writer stages event rows into the pivot's own transaction, and the
        // store is what the messaging OutboxDispatcher polls. The SQL DB owns the outbox table here.
        theServices.AddScoped<OutboxWriter>();
        theServices.AddScoped<IOutboxStore, SqlOutboxStore>();

        return theServices;
    }
}
