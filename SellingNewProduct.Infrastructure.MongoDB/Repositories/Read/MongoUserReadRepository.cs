using Microsoft.EntityFrameworkCore;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Interfaces.Outbound;
using SellingNewProduct.Domain.Users;
using SellingNewProduct.Infrastructure.MongoDB.Mapping;
using SellingNewProduct.Infrastructure.MongoDB.Persistence;

namespace SellingNewProduct.Infrastructure.MongoDB.Repositories.Read;

internal sealed class MongoUserReadRepository : IUserReadRepository
{
    private const int DeletedStatus = (int)EntityStatus.Deleted;

    private readonly MongoAppDbContext myMongoAppDbContext;

    public MongoUserReadRepository(MongoAppDbContext theMongoAppDbContext)
    {
        myMongoAppDbContext = theMongoAppDbContext;
    }

    public async Task<User?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default)
    {
        var aDocument = await myMongoAppDbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == theId && r.Status != DeletedStatus, theCancellationToken);

        return aDocument is null ? null : UserMapper.ToDomain(aDocument);
    }

    public async Task<User?> GetByUsernameAsync(string theUsername, CancellationToken theCancellationToken = default)
    {
        var aDocument = await myMongoAppDbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Username == theUsername && r.Status != DeletedStatus, theCancellationToken);

        return aDocument is null ? null : UserMapper.ToDomain(aDocument);
    }

    public async Task<IReadOnlyList<User>> GetAllAsync(CancellationToken theCancellationToken = default)
    {
        var aDocuments = await myMongoAppDbContext.Users
            .AsNoTracking()
            .Where(r => r.Status != DeletedStatus)
            .ToListAsync(theCancellationToken);

        return aDocuments.Select(UserMapper.ToDomain).ToList();
    }
}
