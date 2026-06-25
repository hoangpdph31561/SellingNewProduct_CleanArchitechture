using Microsoft.EntityFrameworkCore;

namespace SellingNewProduct.Infrastructure.MongoDB.Saga.Persistence;

/// <summary>
/// WRITE-side MongoDB context (targets the PRIMARY). Write repositories inject this. In the saga
/// provider the catalogue/people writes commit immediately and are undone via saga compensations
/// rather than a multi-document Mongo transaction, so a single-node server is sufficient — though
/// the replica-set connection of the standalone Mongo provider still works unchanged.
/// </summary>
public sealed class MongoAppDbContext : MongoDbContextBase
{
    public MongoAppDbContext(DbContextOptions<MongoAppDbContext> theOptions) : base(theOptions)
    {
    }
}
