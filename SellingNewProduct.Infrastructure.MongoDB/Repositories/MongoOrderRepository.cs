using Microsoft.EntityFrameworkCore;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Orders;
using SellingNewProduct.Domain.Repositories;
using SellingNewProduct.Infrastructure.MongoDB.Mapping;
using SellingNewProduct.Infrastructure.MongoDB.Persistence;

namespace SellingNewProduct.Infrastructure.MongoDB.Repositories;

internal sealed class MongoOrderRepository : IOrderRepository
{
    private const int DeletedStatus = (int)EntityStatus.Deleted;

    private readonly MongoAppDbContext myMongoAppDbContext;

    public MongoOrderRepository(MongoAppDbContext theMongoAppDbContext)
    {
        myMongoAppDbContext = theMongoAppDbContext;
    }

    // Details are embedded in the document, so no Include is needed.
    public async Task<Order?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default)
    {
        var aDocument = await myMongoAppDbContext.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == theId && r.Status != DeletedStatus, theCancellationToken);

        return aDocument is null ? null : OrderMapper.ToDomain(aDocument);
    }

    public async Task<IReadOnlyList<Order>> GetByCustomerAsync(Guid theCustomerId, CancellationToken theCancellationToken = default)
    {
        var aDocuments = await myMongoAppDbContext.Orders
            .AsNoTracking()
            .Where(r => r.CustomerId == theCustomerId && r.Status != DeletedStatus)
            .ToListAsync(theCancellationToken);

        return aDocuments.Select(OrderMapper.ToDomain).ToList();
    }

    public async Task<IReadOnlyList<Order>> GetByDateRangeAsync(DateTime theFromUtc, DateTime theToUtc, CancellationToken theCancellationToken = default)
    {
        var aDocuments = await myMongoAppDbContext.Orders
            .AsNoTracking()
            .Where(r => r.OrderDate >= theFromUtc && r.OrderDate <= theToUtc && r.Status != DeletedStatus)
            .ToListAsync(theCancellationToken);

        return aDocuments.Select(OrderMapper.ToDomain).ToList();
    }

    public async Task AddAsync(Order theOrder, CancellationToken theCancellationToken = default)
    {
        myMongoAppDbContext.Orders.Add(OrderMapper.ToDocument(theOrder));
        await myMongoAppDbContext.SaveChangesAsync(theCancellationToken);
    }

    public async Task UpdateAsync(Order theOrder, CancellationToken theCancellationToken = default)
    {
        var aDocument = await myMongoAppDbContext.Orders.FirstOrDefaultAsync(r => r.Id == theOrder.Id, theCancellationToken);

        if (aDocument is null)
        {
            return;
        }

        OrderMapper.MapInto(aDocument, theOrder);
        await myMongoAppDbContext.SaveChangesAsync(theCancellationToken);
    }
}
