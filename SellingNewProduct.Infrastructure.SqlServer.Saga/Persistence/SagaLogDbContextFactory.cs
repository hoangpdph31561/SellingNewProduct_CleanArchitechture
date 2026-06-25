using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SellingNewProduct.Infrastructure.SqlServer.Saga.Persistence;

/// <summary>Design-time factory for EF Core tools (migrations) over the saga-log context.</summary>
public sealed class SagaLogDbContextFactory : IDesignTimeDbContextFactory<SagaLogDbContext>
{
    public SagaLogDbContext CreateDbContext(string[] theArgs)
    {
        const string aConnectionString =
            "Server=(localdb)\\MSSQLLocalDB;Database=SellingNewProduct_Saga;Trusted_Connection=True;TrustServerCertificate=True";

        var aOptions = new DbContextOptionsBuilder<SagaLogDbContext>()
            .UseSqlServer(aConnectionString)
            .Options;

        return new SagaLogDbContext(aOptions);
    }
}
