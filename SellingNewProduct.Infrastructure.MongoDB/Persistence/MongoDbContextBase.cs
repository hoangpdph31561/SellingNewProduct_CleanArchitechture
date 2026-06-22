using Microsoft.EntityFrameworkCore;
using SellingNewProduct.Infrastructure.MongoDB.Models;

namespace SellingNewProduct.Infrastructure.MongoDB.Persistence;

/// <summary>
/// Shared EF Core mapping for the MongoDB infrastructure. Both the write context
/// (<see cref="MongoAppDbContext"/>, talks to the PRIMARY) and the read context
/// (<see cref="MongoReadDbContext"/>, may read from SECONDARIES) derive from this so the
/// document set and configuration live in one place. The two differ only by the connection
/// string's <c>readPreference</c>, wired in DI — never by the model.
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
    internal DbSet<OrderDocument> Orders => Set<OrderDocument>();
    internal DbSet<PaymentDocument> Payments => Set<PaymentDocument>();

    protected override void OnModelCreating(ModelBuilder theModelBuilder)
    {
        theModelBuilder.ApplyConfigurationsFromAssembly(typeof(MongoDbContextBase).Assembly);
        base.OnModelCreating(theModelBuilder);
    }
}
