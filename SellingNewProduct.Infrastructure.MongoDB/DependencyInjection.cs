using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SellingNewProduct.Application.Queries;
using SellingNewProduct.Domain.Repositories;
using SellingNewProduct.Infrastructure.MongoDB.Persistence;
using SellingNewProduct.Infrastructure.MongoDB.Queries;
using SellingNewProduct.Infrastructure.MongoDB.Repositories;

namespace SellingNewProduct.Infrastructure.MongoDB;

/// <summary>
/// Composition for the MongoDB infrastructure. The only place that wires the
/// domain repository interfaces to their MongoDB implementations.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddMongoInfrastructure(this IServiceCollection theServices, IConfiguration theConfiguration)
    {
        var aConnectionString = theConfiguration.GetConnectionString("MongoDB")
            ?? throw new InvalidOperationException("Connection string 'MongoDB' was not found.");

        var aDatabaseName = theConfiguration["MongoDatabaseName"] ?? "SellingNewProduct";

        theServices.AddDbContext<MongoAppDbContext>(o => o.UseMongoDB(aConnectionString, aDatabaseName));

        theServices.AddScoped<IUserRepository, MongoUserRepository>();
        theServices.AddScoped<ICustomerRepository, MongoCustomerRepository>();
        theServices.AddScoped<IEmployeeRepository, MongoEmployeeRepository>();
        theServices.AddScoped<ICategoryRepository, MongoCategoryRepository>();
        theServices.AddScoped<IProductRepository, MongoProductRepository>();
        theServices.AddScoped<IOrderRepository, MongoOrderRepository>();
        theServices.AddScoped<IPaymentRepository, MongoPaymentRepository>();

        // Read side (CQRS-lite): Mongo cannot JOIN, so it stitches in memory.
        theServices.AddScoped<IOrderQueries, MongoOrderQueries>();
        theServices.AddScoped<IReportQueries, MongoReportQueries>();

        return theServices;
    }
}
