using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SellingNewProduct.Infrastructure.SqlServer.Persistence;

/// <summary>
/// Design-time factory used by the EF Core tools (migrations). Runtime uses the
/// connection string from appsettings via DependencyInjection instead.
/// </summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] theArgs)
    {
        const string aConnectionString =
            "Server=(localdb)\\MSSQLLocalDB;Database=SellingNewProduct;Trusted_Connection=True;TrustServerCertificate=True";

        var aOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(aConnectionString)
            .Options;

        return new AppDbContext(aOptions);
    }
}
