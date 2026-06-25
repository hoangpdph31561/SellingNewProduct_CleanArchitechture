using Microsoft.EntityFrameworkCore;
using SellingNewProduct.Infrastructure.SqlServer.Saga.Configurations;
using SellingNewProduct.Infrastructure.SqlServer.Saga.Models;

namespace SellingNewProduct.Infrastructure.SqlServer.Saga.Persistence;

/// <summary>
/// EF Core context for the SQL side of the saga provider. It owns ONLY the aggregates assigned to
/// SQL Server — Orders (+ OrderDetails) and Payments. The catalogue and people aggregates live in
/// MongoDB, so they are not mapped here. The saga log lives in its own <see cref="SagaLogDbContext"/>
/// so it survives a compensated saga.
/// </summary>
public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> theOptions) : base(theOptions)
    {
    }

    internal DbSet<OrderRecord> Orders => Set<OrderRecord>();
    internal DbSet<OrderDetailRecord> OrderDetails => Set<OrderDetailRecord>();
    internal DbSet<PaymentRecord> Payments => Set<PaymentRecord>();

    // Saga pivot-commit markers, written atomically with the business commit (used by recovery).
    internal DbSet<SagaCommitRecord> SagaCommits => Set<SagaCommitRecord>();

    protected override void OnModelCreating(ModelBuilder theModelBuilder)
    {
        theModelBuilder.ApplyConfiguration(new OrderConfiguration());
        theModelBuilder.ApplyConfiguration(new OrderDetailConfiguration());
        theModelBuilder.ApplyConfiguration(new PaymentConfiguration());
        theModelBuilder.ApplyConfiguration(new SagaCommitConfiguration());
        base.OnModelCreating(theModelBuilder);
    }
}
