using Microsoft.EntityFrameworkCore;

namespace SellingNewProduct.Infrastructure.MongoDB.Persistence;

/// <summary>
/// WRITE-side EF Core context for the MongoDB infrastructure. Its connection string targets the
/// PRIMARY (<c>readPreference=primary</c>) because all writes and multi-document transactions
/// (<see cref="MongoUnitOfWork"/>) must run on the primary. Write repositories inject this.
/// Shares its document mapping with the read context via <see cref="MongoDbContextBase"/>.
/// </summary>
public sealed class MongoAppDbContext : MongoDbContextBase
{
    public MongoAppDbContext(DbContextOptions<MongoAppDbContext> theOptions) : base(theOptions)
    {
    }
}
