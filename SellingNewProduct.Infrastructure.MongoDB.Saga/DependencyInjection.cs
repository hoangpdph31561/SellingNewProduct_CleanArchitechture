using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SellingNewProduct.Domain.Interfaces.Outbound;
using SellingNewProduct.Infrastructure.MongoDB.Saga.Persistence;
using SellingNewProduct.Infrastructure.MongoDB.Saga.Repositories.Read;
using SellingNewProduct.Infrastructure.MongoDB.Saga.Repositories.Write;
using SellingNewProduct.Infrastructure.MongoDB.Saga.Saga;
using SellingNewProduct.Infrastructure.Saga.Core;
using SellingNewProduct.Infrastructure.Saga.Core.CrossDb;
using SellingNewProduct.Infrastructure.Saga.Core.Saga;

namespace SellingNewProduct.Infrastructure.MongoDB.Saga;

/// <summary>
/// Composition for the MongoDB side of the saga (hybrid) provider. Owns the catalogue + people
/// aggregates (Product/Category/Customer/Employee/User), the saga-aware product writes, MongoDB's
/// saga participant, and the cross-store <see cref="ICrossDbDirectory"/> the SQL read models use.
/// Register alongside <c>AddSagaCore</c> and <c>AddSqlServerSagaInfrastructure</c> in the
/// composition root.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddMongoSagaInfrastructure(this IServiceCollection theServices, IConfiguration theConfiguration)
    {
        var aConnectionString = theConfiguration.GetConnectionString("MongoDB")
            ?? throw new InvalidOperationException("Connection string 'MongoDB' was not found.");

        var aReadConnectionString = theConfiguration.GetConnectionString("MongoDBRead") ?? aConnectionString;

        var aDatabaseName = theConfiguration["MongoDatabaseName"] ?? "SellingNewProduct";

        theServices.AddDbContext<MongoAppDbContext>(o => o.UseMongoDB(aConnectionString, aDatabaseName));
        theServices.AddDbContext<MongoReadDbContext>(o => o.UseMongoDB(aReadConnectionString, aDatabaseName));

        // Write side.
        theServices.AddScoped<IProductWriteRepository, MongoProductWriteRepository>();
        theServices.AddScoped<ICategoryWriteRepository, MongoCategoryWriteRepository>();
        theServices.AddScoped<ICustomerWriteRepository, MongoCustomerWriteRepository>();
        theServices.AddScoped<IEmployeeWriteRepository, MongoEmployeeWriteRepository>();
        theServices.AddScoped<IUserWriteRepository, MongoUserWriteRepository>();

        // Read side.
        theServices.AddScoped<IProductReadRepository, MongoProductReadRepository>();
        theServices.AddScoped<ICategoryReadRepository, MongoCategoryReadRepository>();
        theServices.AddScoped<ICustomerReadRepository, MongoCustomerReadRepository>();
        theServices.AddScoped<IEmployeeReadRepository, MongoEmployeeReadRepository>();
        theServices.AddScoped<IUserReadRepository, MongoUserReadRepository>();

        // The catalogue/people directory for the SQL read models.
        theServices.AddScoped<ICrossDbDirectory, MongoCrossDbDirectory>();

        // MongoDB owns the single saga ledger. The store is shared by the saga-aware repositories
        // (so a step commits atomically with its ledger entry), so register the concrete type too.
        theServices.AddScoped<MongoSagaStore>();
        theServices.AddScoped<ISagaStore>(sp => sp.GetRequiredService<MongoSagaStore>());

        // Auto-register every ISagaCompensationHandler in THIS assembly (stock today, more later) —
        // a new handler class is picked up with no extra registration line.
        theServices.AddSagaCompensationHandlers(typeof(DependencyInjection).Assembly);

        return theServices;
    }
}
