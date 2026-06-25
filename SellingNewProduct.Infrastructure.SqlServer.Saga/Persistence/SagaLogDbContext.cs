using Microsoft.EntityFrameworkCore;
using SellingNewProduct.Infrastructure.SqlServer.Saga.Configurations;
using SellingNewProduct.Infrastructure.SqlServer.Saga.Models;

namespace SellingNewProduct.Infrastructure.SqlServer.Saga.Persistence;

/// <summary>
/// A SEPARATE EF Core context dedicated to the saga ledger. Kept apart from
/// <see cref="AppDbContext"/> on purpose: the saga log must be written on its own connection and
/// transaction so that when a saga compensates (and the business <see cref="AppDbContext"/>
/// transaction rolls back), the "Compensated" log row still persists.
/// </summary>
public sealed class SagaLogDbContext : DbContext
{
    public SagaLogDbContext(DbContextOptions<SagaLogDbContext> theOptions) : base(theOptions)
    {
    }

    internal DbSet<SagaTransactionRecord> SagaTransactions => Set<SagaTransactionRecord>();

    protected override void OnModelCreating(ModelBuilder theModelBuilder)
    {
        theModelBuilder.ApplyConfiguration(new SagaTransactionConfiguration());
        base.OnModelCreating(theModelBuilder);
    }
}
