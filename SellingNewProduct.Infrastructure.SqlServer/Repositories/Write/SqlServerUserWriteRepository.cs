using Microsoft.EntityFrameworkCore;
using SellingNewProduct.Domain.Interfaces.Outbound;
using SellingNewProduct.Domain.Users;
using SellingNewProduct.Infrastructure.SqlServer.Mapping;
using SellingNewProduct.Infrastructure.SqlServer.Persistence;

namespace SellingNewProduct.Infrastructure.SqlServer.Repositories.Write;

internal sealed class SqlServerUserWriteRepository : IUserWriteRepository
{
    private readonly AppDbContext myAppDbContext;

    public SqlServerUserWriteRepository(AppDbContext theAppDbContext)
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
