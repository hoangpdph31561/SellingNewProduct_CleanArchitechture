using Microsoft.EntityFrameworkCore;
using SellingNewProduct.Infrastructure.MongoDB.Saga.Models;

namespace SellingNewProduct.Infrastructure.MongoDB.Saga.Persistence;

/// <summary>
/// Shared EF Core mapping for the MongoDB side of the saga provider. Maps ONLY the aggregates
/// assigned to MongoDB — Products, Categories, Customers, Employees, Users. Orders and Payments
/// live in SQL Server here, so they are not mapped. Both the write context
/// (<see cref="MongoAppDbContext"/>, primary) and the read context (<see cref="MongoReadDbContext"/>,
/// secondaries) derive from this so the document set lives in one place.
/// </summary>
public abstract class MongoDbContextBase : DbContext
{
    protected MongoDbContextBase(DbContextOptions theOptions) : base(theOptions)
    {
    }

    internal DbSet<UserDocument> Users => Set<UserDocument>();
    internal DbSet<CustomerDocument> Customers => Set<CustomerDocument>();
    internal DbSet<EmployeeDocument> Employees => Set<EmployeeDocument>();
    internal DbSet<CategoryDocument> Categories => Set<CategoryDocument>();
    internal DbSet<ProductDocument> Products => Set<ProductDocument>();

    // The single durable saga ledger (one document per saga). Drives rollback and crash recovery.
    internal DbSet<SagaInstanceDocument> SagaInstances => Set<SagaInstanceDocument>();

    // The MongoDB-side transactional outbox (catalogue events, e.g. stock movements).
    internal DbSet<OutboxMessageDocument> OutboxMessages => Set<OutboxMessageDocument>();

    protected override void OnModelCreating(ModelBuilder theModelBuilder)
    {
        theModelBuilder.ApplyConfigurationsFromAssembly(typeof(MongoDbContextBase).Assembly);
        base.OnModelCreating(theModelBuilder);
    }
}
