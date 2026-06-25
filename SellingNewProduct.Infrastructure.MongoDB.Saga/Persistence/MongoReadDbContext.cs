using Microsoft.EntityFrameworkCore;

namespace SellingNewProduct.Infrastructure.MongoDB.Saga.Persistence;

/// <summary>
/// READ-side MongoDB context. Its connection string may use <c>readPreference=secondaryPreferred</c>
/// (config key <c>ConnectionStrings:MongoDBRead</c>) so queries can be served by secondaries on a
/// multi-node replica set — the same CQRS read/write split as the standalone Mongo provider. On a
/// single node it simply reads the primary.
/// </summary>
public sealed class MongoReadDbContext : MongoDbContextBase
{
    public MongoReadDbContext(DbContextOptions<MongoReadDbContext> theOptions) : base(theOptions)
    {
    }
}
