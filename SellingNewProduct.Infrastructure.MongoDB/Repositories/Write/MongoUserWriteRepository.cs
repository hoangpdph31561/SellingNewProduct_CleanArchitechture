using Microsoft.EntityFrameworkCore;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Interfaces.Outbound;
using SellingNewProduct.Domain.Users;
using SellingNewProduct.Infrastructure.MongoDB.Mapping;
using SellingNewProduct.Infrastructure.MongoDB.Persistence;

namespace SellingNewProduct.Infrastructure.MongoDB.Repositories.Write;

internal sealed class MongoUserWriteRepository : IUserWriteRepository
{
    private const int DeletedStatus = (int)EntityStatus.Deleted;

    private readonly MongoAppDbContext myMongoAppDbContext;

    public MongoUserWriteRepository(MongoAppDbContext theMongoAppDbContext)
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
