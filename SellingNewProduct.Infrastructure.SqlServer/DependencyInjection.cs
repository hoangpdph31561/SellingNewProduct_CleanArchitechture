using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SellingNewProduct.Domain.Interfaces.Outbound;
using SellingNewProduct.Infrastructure.SqlServer.Persistence;
using SellingNewProduct.Infrastructure.SqlServer.Repositories.Read;
using SellingNewProduct.Infrastructure.SqlServer.Repositories.Write;

namespace SellingNewProduct.Infrastructure.SqlServer;

/// <summary>
/// Composition for the SQL Server infrastructure. Wires the domain read/write repository
/// interfaces to their SQL Server implementations plus the SQL Server unit of work. The read
/// and write sides are deliberately separate classes (CQRS at the persistence layer).
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddSqlServerInfrastructure(this IServiceCollection theServices, IConfiguration theConfiguration)
    {
        var aConnectionString = theConfiguration.GetConnectionString("SqlServer")
            ?? throw new InvalidOperationException("Connection string 'SqlServer' was not found.");

        theServices.AddDbContext<AppDbContext>(o => o.UseSqlServer(aConnectionString));

        // Write side (command handlers).
        theServices.AddScoped<IUserWriteRepository, SqlServerUserWriteRepository>();
        theServices.AddScoped<ICustomerWriteRepository, SqlServerCustomerWriteRepository>();
        theServices.AddScoped<IEmployeeWriteRepository, SqlServerEmployeeWriteRepository>();
        theServices.AddScoped<ICategoryWriteRepository, SqlServerCategoryWriteRepository>();
        theServices.AddScoped<IProductWriteRepository, SqlServerProductWriteRepository>();
        theServices.AddScoped<IOrderWriteRepository, SqlServerOrderWriteRepository>();
        theServices.AddScoped<IPaymentWriteRepository, SqlServerPaymentWriteRepository>();

        // Read side (query handlers).
        theServices.AddScoped<IUserReadRepository, SqlServerUserReadRepository>();
        theServices.AddScoped<ICustomerReadRepository, SqlServerCustomerReadRepository>();
        theServices.AddScoped<IEmployeeReadRepository, SqlServerEmployeeReadRepository>();
        theServices.AddScoped<ICategoryReadRepository, SqlServerCategoryReadRepository>();
        theServices.AddScoped<IProductReadRepository, SqlServerProductReadRepository>();
        theServices.AddScoped<IOrderReadRepository, SqlServerOrderReadRepository>();
        theServices.AddScoped<IPaymentReadRepository, SqlServerPaymentReadRepository>();
        theServices.AddScoped<IReportReadRepository, SqlServerReportReadRepository>();

        // Unit of work: groups several write repositories into one atomic transaction.
        theServices.AddScoped<IUnitOfWork, SqlServerUnitOfWork>();

        return theServices;
    }
}
