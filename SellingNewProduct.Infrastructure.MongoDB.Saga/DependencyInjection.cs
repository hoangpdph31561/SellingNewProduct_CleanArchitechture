using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SellingNewProduct.Domain.Interfaces.Outbound;
using SellingNewProduct.Infrastructure.MongoDB.Saga.Persistence;
using SellingNewProduct.Infrastructure.MongoDB.Saga.Repositories.Read;
using SellingNewProduct.Infrastructure.MongoDB.Saga.Repositories.Write;
using SellingNewProduct.Infrastructure.MongoDB.Saga.Saga;
using SellingNewProduct.Infrastructure.Saga.Core.CrossDb;
using SellingNewProduct.Infrastructure.Saga.Core.Recovery;
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

        // Saga: MongoDB transaction participant and the catalogue/people directory for SQL read models.
        theServices.AddScoped<ISagaParticipant, MongoSagaParticipant>();
        theServices.AddScoped<ICrossDbDirectory, MongoCrossDbDirectory>();

        // Durable saga-effect store: written by the product repo, reverted in-process and by recovery.
        theServices.AddScoped<MongoSagaEffectStore>();
        theServices.AddScoped<ISagaEffectStore>(sp => sp.GetRequiredService<MongoSagaEffectStore>());

        return theServices;
    }
}
