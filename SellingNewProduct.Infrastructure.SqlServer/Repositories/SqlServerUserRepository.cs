using Microsoft.EntityFrameworkCore;
using SellingNewProduct.Domain.Repositories;
using SellingNewProduct.Domain.Users;
using SellingNewProduct.Infrastructure.SqlServer.Mapping;
using SellingNewProduct.Infrastructure.SqlServer.Persistence;

namespace SellingNewProduct.Infrastructure.SqlServer.Repositories;

internal sealed class SqlServerUserRepository : IUserRepository
{
    private readonly AppDbContext myAppDbContext;

    public SqlServerUserRepository(AppDbContext theAppDbContext)
    {
        myAppDbContext = theAppDbContext;
    }

    public async Task<User?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default)
    {
        var aRecord = await myAppDbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == theId, theCancellationToken);

        return aRecord is null ? null : UserMapper.ToDomain(aRecord);
    }

    public async Task<User?> GetByUsernameAsync(string theUsername, CancellationToken theCancellationToken = default)
    {
        var aRecord = await myAppDbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Username == theUsername, theCancellationToken);

        return aRecord is null ? null : UserMapper.ToDomain(aRecord);
    }

    public async Task<IReadOnlyList<User>> GetAllAsync(CancellationToken theCancellationToken = default)
    {
        var aRecords = await myAppDbContext.Users.AsNoTracking().ToListAsync(theCancellationToken);
        return aRecords.Select(UserMapper.ToDomain).ToList();
    }

    public async Task AddAsync(User theUser, CancellationToken theCancellationToken = default)
    {
        myAppDbContext.Users.Add(UserMapper.ToRecord(theUser));
        await myAppDbContext.SaveChangesAsync(theCancellationToken);
    }

    public async Task UpdateAsync(User theUser, CancellationToken theCancellationToken = default)
    {
        var aRecord = await myAppDbContext.Users.FirstOrDefaultAsync(r => r.Id == theUser.Id, theCancellationToken);

        if (aRecord is null)
        {
            return;
        }

        UserMapper.MapInto(aRecord, theUser);
        await myAppDbContext.SaveChangesAsync(theCancellationToken);
    }
}
