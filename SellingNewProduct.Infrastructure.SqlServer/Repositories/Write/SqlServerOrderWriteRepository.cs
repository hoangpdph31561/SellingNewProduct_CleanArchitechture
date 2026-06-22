using Microsoft.EntityFrameworkCore;
using SellingNewProduct.Domain.Orders;
using SellingNewProduct.Domain.Interfaces.Outbound;
using SellingNewProduct.Infrastructure.SqlServer.Mapping;
using SellingNewProduct.Infrastructure.SqlServer.Persistence;

namespace SellingNewProduct.Infrastructure.SqlServer.Repositories.Write;

internal sealed class SqlServerOrderWriteRepository : IOrderWriteRepository
{
    private readonly AppDbContext myAppDbContext;

    public SqlServerOrderWriteRepository(AppDbContext theAppDbContext)
    {
        myAppDbContext = theAppDbContext;
    }

    public async Task<Order?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default)
    {
        var aRecord = await myAppDbContext.Orders
            .AsNoTracking()
            .Include(r => r.Details)
            .FirstOrDefaultAsync(r => r.Id == theId, theCancellationToken);

        return aRecord is null ? null : OrderMapper.ToDomain(aRecord);
    }

    public async Task AddAsync(Order theOrder, CancellationToken theCancellationToken = default)
    {
        myAppDbContext.Orders.Add(OrderMapper.ToRecord(theOrder));
        await myAppDbContext.SaveChangesAsync(theCancellationToken);
    }

    public async Task UpdateAsync(Order theOrder, CancellationToken theCancellationToken = default)
    {
        var aRecord = await myAppDbContext.Orders
            .Include(r => r.Details)
            .FirstOrDefaultAsync(r => r.Id == theOrder.Id, theCancellationToken);

        if (aRecord is null)
        {
            return;
        }

        OrderMapper.MapInto(aRecord, theOrder);
        await myAppDbContext.SaveChangesAsync(theCancellationToken);
    }
}
