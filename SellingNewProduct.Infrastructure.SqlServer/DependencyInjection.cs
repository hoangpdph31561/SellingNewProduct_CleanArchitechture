using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SellingNewProduct.Application.Queries;
using SellingNewProduct.Domain.Repositories;
using SellingNewProduct.Infrastructure.SqlServer.Persistence;
using SellingNewProduct.Infrastructure.SqlServer.Queries;
using SellingNewProduct.Infrastructure.SqlServer.Repositories;

namespace SellingNewProduct.Infrastructure.SqlServer;

/// <summary>
/// Composition for the SQL Server infrastructure. This is the only place that
/// wires the domain repository interfaces to their SQL Server implementations.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddSqlServerInfrastructure(this IServiceCollection theServices, IConfiguration theConfiguration)
    {
        var aConnectionString = theConfiguration.GetConnectionString("SqlServer")
            ?? throw new InvalidOperationException("Connection string 'SqlServer' was not found.");

        theServices.AddDbContext<AppDbContext>(o => o.UseSqlServer(aConnectionString));

        theServices.AddScoped<IUserRepository, SqlServerUserRepository>();
        theServices.AddScoped<ICustomerRepository, SqlServerCustomerRepository>();
        theServices.AddScoped<IEmployeeRepository, SqlServerEmployeeRepository>();
        theServices.AddScoped<ICategoryRepository, SqlServerCategoryRepository>();
        theServices.AddScoped<IProductRepository, SqlServerProductRepository>();
        theServices.AddScoped<IOrderRepository, SqlServerOrderRepository>();
        theServices.AddScoped<IPaymentRepository, SqlServerPaymentRepository>();

        // Read side (CQRS-lite): JOIN/reporting queries, separate from the write repositories.
        theServices.AddScoped<IOrderQueries, SqlServerOrderQueries>();
        theServices.AddScoped<IReportQueries, SqlServerReportQueries>();

        return theServices;
    }
}
