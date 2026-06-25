using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SellingNewProduct.Infrastructure.SqlServer.Saga.Persistence;

/// <summary>Design-time factory for EF Core tools (migrations) over the business context.</summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] theArgs)
    {
        const string aConnectionString =
            "Server=(localdb)\\MSSQLLocalDB;Database=SellingNewProduct_Saga;Trusted_Connection=True;TrustServerCertificate=True";

        var aOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(aConnectionString)
            .Options;

        return new AppDbContext(aOptions);
    }
}
