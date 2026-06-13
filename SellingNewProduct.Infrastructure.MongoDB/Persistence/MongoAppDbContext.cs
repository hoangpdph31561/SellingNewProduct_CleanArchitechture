using Microsoft.EntityFrameworkCore;
using SellingNewProduct.Infrastructure.MongoDB.Models;

namespace SellingNewProduct.Infrastructure.MongoDB.Persistence;

/// <summary>
/// EF Core context for the MongoDB infrastructure. Knows only about the Mongo
/// persistence models (<c>*Document</c>), never about domain entities.
/// </summary>
public sealed class MongoAppDbContext : DbContext
{
    public MongoAppDbContext(DbContextOptions<MongoAppDbContext> theOptions) : base(theOptions)
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
        theModelBuilder.ApplyConfigurationsFromAssembly(typeof(MongoAppDbContext).Assembly);
        base.OnModelCreating(theModelBuilder);
    }
}
