using Microsoft.EntityFrameworkCore;
using SellingNewProduct.Domain.Orders;
using SellingNewProduct.Domain.Repositories;
using SellingNewProduct.Infrastructure.SqlServer.Mapping;
using SellingNewProduct.Infrastructure.SqlServer.Persistence;

namespace SellingNewProduct.Infrastructure.SqlServer.Repositories;

internal sealed class SqlServerOrderRepository : IOrderRepository
{
    private readonly AppDbContext myAppDbContext;

    public SqlServerOrderRepository(AppDbContext theAppDbContext)
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

    public async Task<IReadOnlyList<Order>> GetByCustomerAsync(Guid theCustomerId, CancellationToken theCancellationToken = default)
    {
        var aRecords = await myAppDbContext.Orders
            .AsNoTracking()
            .Include(r => r.Details)
            .Where(r => r.CustomerId == theCustomerId)
            .ToListAsync(theCancellationToken);

        return aRecords.Select(OrderMapper.ToDomain).ToList();
    }

    public async Task<IReadOnlyList<Order>> GetByDateRangeAsync(DateTime theFromUtc, DateTime theToUtc, CancellationToken theCancellationToken = default)
    {
        var aRecords = await myAppDbContext.Orders
            .AsNoTracking()
            .Include(r => r.Details)
            .Where(r => r.OrderDate >= theFromUtc && r.OrderDate <= theToUtc)
            .ToListAsync(theCancellationToken);

        return aRecords.Select(OrderMapper.ToDomain).ToList();
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
