using Microsoft.EntityFrameworkCore;

namespace SellingNewProduct.Infrastructure.MongoDB.Persistence;

/// <summary>
/// READ-side EF Core context for the MongoDB infrastructure. Its connection string uses
/// <c>readPreference=secondaryPreferred</c> (config key <c>ConnectionStrings:MongoDBRead</c>), so
/// queries are served by SECONDARIES when the cluster is a multi-node replica set — offloading the
/// primary. Read repositories inject this context. On a single-node set it simply reads the primary.
///
/// ⚠️ Trade-off: secondaries lag behind the primary (eventual consistency), so a read right after a
/// write may return stale data. Acceptable for list/report views; not for read-your-own-write needs.
/// </summary>
public sealed class MongoReadDbContext : MongoDbContextBase
{
    public MongoReadDbContext(DbContextOptions<MongoReadDbContext> theOptions) : base(theOptions)
    {
    }
}
