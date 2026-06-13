using Microsoft.EntityFrameworkCore;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Repositories;
using SellingNewProduct.Domain.Users;
using SellingNewProduct.Infrastructure.MongoDB.Mapping;
using SellingNewProduct.Infrastructure.MongoDB.Persistence;

namespace SellingNewProduct.Infrastructure.MongoDB.Repositories;

internal sealed class MongoUserRepository : IUserRepository
{
    // MongoDB provider has no global query filter, so soft delete is filtered here.
    private const int DeletedStatus = (int)EntityStatus.Deleted;

    private readonly MongoAppDbContext myMongoAppDbContext;

    public MongoUserRepository(MongoAppDbContext theMongoAppDbContext)
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

    public async Task AddAsync(User theUser, CancellationToken theCancellationToken = default)
    {
        myMongoAppDbContext.Users.Add(UserMapper.ToDocument(theUser));
        await myMongoAppDbContext.SaveChangesAsync(theCancellationToken);
    }

    public async Task UpdateAsync(User theUser, CancellationToken theCancellationToken = default)
    {
        var aDocument = await myMongoAppDbContext.Users.FirstOrDefaultAsync(r => r.Id == theUser.Id, theCancellationToken);

        if (aDocument is null)
        {
            return;
        }

        UserMapper.MapInto(aDocument, theUser);
        await myMongoAppDbContext.SaveChangesAsync(theCancellationToken);
    }
}
