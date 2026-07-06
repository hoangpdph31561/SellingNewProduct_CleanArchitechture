using Microsoft.EntityFrameworkCore;
using SellingNewProduct.Infrastructure.SqlServer.Saga.Configurations;
using SellingNewProduct.Infrastructure.SqlServer.Saga.Models;

namespace SellingNewProduct.Infrastructure.SqlServer.Saga.Persistence;

/// <summary>
/// EF Core context for the SQL side of the saga provider. It owns ONLY the aggregates assigned to
/// SQL Server — Orders (+ OrderDetails) and Payments. The catalogue and people aggregates live in
/// MongoDB. There is no saga bookkeeping here anymore: the single saga ledger lives in MongoDB
/// (<c>saga_instances</c>), so SQL just commits its Order/Payment writes immediately as one saga step.
/// </summary>
public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> theOptions) : base(theOptions)
    {
    }

    internal DbSet<OrderRecord> Orders => Set<OrderRecord>();
    internal DbSet<OrderDetailRecord> OrderDetails => Set<OrderDetailRecord>();
    internal DbSet<PaymentRecord> Payments => Set<PaymentRecord>();

    // The transactional outbox. It lives in the SAME context so an event row commits atomically with
    // the Order/Payment write that raised it — the guarantee the whole outbox pattern rests on.
    internal DbSet<OutboxMessageRecord> OutboxMessages => Set<OutboxMessageRecord>();

    protected override void OnModelCreating(ModelBuilder theModelBuilder)
    {
        theModelBuilder.ApplyConfiguration(new OrderConfiguration());
        theModelBuilder.ApplyConfiguration(new OrderDetailConfiguration());
        theModelBuilder.ApplyConfiguration(new PaymentConfiguration());
        theModelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
        base.OnModelCreating(theModelBuilder);
    }
}
