using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SellingNewProduct.Infrastructure.SqlServer.Models;

namespace SellingNewProduct.Infrastructure.SqlServer.Persistence;

/// <summary>
/// EF Core context for the SQL Server infrastructure. It only knows about the
/// SQL persistence models (<c>*Record</c>), never about domain entities.
/// </summary>
public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> theOptions) : base(theOptions)
    {
    }

    internal DbSet<UserRecord> Users => Set<UserRecord>();
    internal DbSet<CustomerRecord> Customers => Set<CustomerRecord>();
    internal DbSet<EmployeeRecord> Employees => Set<EmployeeRecord>();
    internal DbSet<CategoryRecord> Categories => Set<CategoryRecord>();
    internal DbSet<ProductRecord> Products => Set<ProductRecord>();
    internal DbSet<OrderRecord> Orders => Set<OrderRecord>();
    internal DbSet<OrderDetailRecord> OrderDetails => Set<OrderDetailRecord>();
    internal DbSet<PaymentRecord> Payments => Set<PaymentRecord>();

    protected override void OnModelCreating(ModelBuilder theModelBuilder)
    {
        // Picks up every IEntityTypeConfiguration in this assembly.
        theModelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(theModelBuilder);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder theOptionsBuilder)
    {
        // Soft-delete query filters on both ends of FK relationships are intentional here.
        theOptionsBuilder.ConfigureWarnings(w =>
            w.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning));
        base.OnConfiguring(theOptionsBuilder);
    }
}
