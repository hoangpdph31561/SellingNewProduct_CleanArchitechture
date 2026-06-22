using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SellingNewProduct.Domain.Interfaces.Outbound;
using SellingNewProduct.Infrastructure.MongoDB.Persistence;
using SellingNewProduct.Infrastructure.MongoDB.Repositories.Read;
using SellingNewProduct.Infrastructure.MongoDB.Repositories.Write;

namespace SellingNewProduct.Infrastructure.MongoDB;

/// <summary>
/// Composition for the MongoDB infrastructure. Wires the domain read/write repository interfaces
/// to their MongoDB implementations plus the MongoDB unit of work. Read and write are separate
/// classes (CQRS at the persistence layer). The unit of work needs a replica set connection to
/// run transactions (see docs/mongo-replica-set.md).
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddMongoInfrastructure(this IServiceCollection theServices, IConfiguration theConfiguration)
    {
        var aConnectionString = theConfiguration.GetConnectionString("MongoDB")
            ?? throw new InvalidOperationException("Connection string 'MongoDB' was not found.");

        // Read connection (readPreference=secondaryPreferred) so queries can be served by secondaries
        // on a multi-node replica set. Optional: falls back to the primary connection when absent,
        // which keeps single-node / SqlServer setups working unchanged.
        var aReadConnectionString = theConfiguration.GetConnectionString("MongoDBRead") ?? aConnectionString;

        var aDatabaseName = theConfiguration["MongoDatabaseName"] ?? "SellingNewProduct";

        // Write/primary context (writes + transactions) and read/secondary context.
        theServices.AddDbContext<MongoAppDbContext>(o => o.UseMongoDB(aConnectionString, aDatabaseName));
        theServices.AddDbContext<MongoReadDbContext>(o => o.UseMongoDB(aReadConnectionString, aDatabaseName));

        // Write side (command handlers).
        theServices.AddScoped<IUserWriteRepository, MongoUserWriteRepository>();
        theServices.AddScoped<ICustomerWriteRepository, MongoCustomerWriteRepository>();
        theServices.AddScoped<IEmployeeWriteRepository, MongoEmployeeWriteRepository>();
        theServices.AddScoped<ICategoryWriteRepository, MongoCategoryWriteRepository>();
        theServices.AddScoped<IProductWriteRepository, MongoProductWriteRepository>();
        theServices.AddScoped<IOrderWriteRepository, MongoOrderWriteRepository>();
        theServices.AddScoped<IPaymentWriteRepository, MongoPaymentWriteRepository>();

        // Read side (query handlers).
        theServices.AddScoped<IUserReadRepository, MongoUserReadRepository>();
        theServices.AddScoped<ICustomerReadRepository, MongoCustomerReadRepository>();
        theServices.AddScoped<IEmployeeReadRepository, MongoEmployeeReadRepository>();
        theServices.AddScoped<ICategoryReadRepository, MongoCategoryReadRepository>();
        theServices.AddScoped<IProductReadRepository, MongoProductReadRepository>();
        theServices.AddScoped<IOrderReadRepository, MongoOrderReadRepository>();
        theServices.AddScoped<IPaymentReadRepository, MongoPaymentReadRepository>();
        theServices.AddScoped<IReportReadRepository, MongoReportReadRepository>();

        // Unit of work: multi-document transaction — requires a MongoDB replica set.
        theServices.AddScoped<IUnitOfWork, MongoUnitOfWork>();

        return theServices;
    }
}
