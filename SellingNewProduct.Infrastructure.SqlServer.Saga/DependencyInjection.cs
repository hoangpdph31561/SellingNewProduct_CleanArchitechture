using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SellingNewProduct.Domain.Interfaces.Outbound;
using SellingNewProduct.Infrastructure.Saga.Core.CrossDb;
using SellingNewProduct.Infrastructure.Saga.Core.Recovery;
using SellingNewProduct.Infrastructure.Saga.Core.Saga;
using SellingNewProduct.Infrastructure.SqlServer.Saga.Persistence;
using SellingNewProduct.Infrastructure.SqlServer.Saga.Repositories.Read;
using SellingNewProduct.Infrastructure.SqlServer.Saga.Repositories.Write;
using SellingNewProduct.Infrastructure.SqlServer.Saga.Saga;

namespace SellingNewProduct.Infrastructure.SqlServer.Saga;

/// <summary>
/// Composition for the SQL side of the saga (hybrid) provider. Owns the Order/Payment aggregates
/// plus the saga's SQL pivot (<see cref="SqlSagaParticipant"/>) and the durable saga ledger
/// (<see cref="SqlSagaLog"/>). The cross-database read repositories depend on the MongoDB read
/// ports, which are registered by the MongoDB saga project — register both alongside
/// <c>AddSagaCore</c> in the composition root.
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

        // Business context (Orders/Payments) and the SEPARATE saga-log context (its own connection,
        // so the ledger survives a compensated business transaction).
        theServices.AddDbContext<AppDbContext>(o => o.UseSqlServer(aConnectionString));
        theServices.AddDbContext<SagaLogDbContext>(o => o.UseSqlServer(aConnectionString));

        // Write side (SQL aggregates).
        theServices.AddScoped<IOrderWriteRepository, SqlOrderWriteRepository>();
        theServices.AddScoped<IPaymentWriteRepository, SqlPaymentWriteRepository>();

        // Read side (SQL aggregation + in-memory enrichment from MongoDB read ports).
        theServices.AddScoped<IOrderReadRepository, SqlOrderReadRepository>();
        theServices.AddScoped<IPaymentReadRepository, SqlPaymentReadRepository>();
        theServices.AddScoped<IReportReadRepository, SqlReportReadRepository>();

        // Saga: the SQL pivot participant and the durable log (replaces the kernel's NullSagaLog).
        theServices.AddScoped<ISagaParticipant, SqlSagaParticipant>();
        theServices.AddScoped<ISagaLog, SqlSagaLog>();

        // SQL-backed order statistics consumed by the MongoDB people read models (cycle-free leaf).
        theServices.AddScoped<ICrossDbOrderStats, SqlCrossDbOrderStats>();

        // Pivot-commit marker store for the crash-recovery worker.
        theServices.AddScoped<ISagaCommitStore, SqlSagaCommitStore>();

        return theServices;
    }
}
