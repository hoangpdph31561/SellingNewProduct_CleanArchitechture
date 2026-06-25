using Microsoft.EntityFrameworkCore;
using SellingNewProduct.Domain.Orders;
using SellingNewProduct.Infrastructure.Saga.Core.CrossDb;
using SellingNewProduct.Infrastructure.SqlServer.Saga.Persistence;

namespace SellingNewProduct.Infrastructure.SqlServer.Saga.Saga;

/// <summary>
/// SQL-backed implementation of <see cref="ICrossDbOrderStats"/>: the order statistics that the
/// MongoDB people read models need (orders live in SQL here). It depends only on
/// <see cref="AppDbContext"/> — never on a MongoDB read port — which is what keeps the cross-store
/// read graph free of dependency cycles.
/// </summary>
internal sealed class SqlCrossDbOrderStats : ICrossDbOrderStats
{
    private readonly AppDbContext myAppDbContext;

    public SqlCrossDbOrderStats(AppDbContext theAppDbContext)
    {
        myAppDbContext = theAppDbContext;
    }

    public Task<int> CountSalesOrdersForEmployeeAsync(Guid theEmployeeId, CancellationToken theCancellationToken = default)
    {
        return myAppDbContext.Orders.AsNoTracking()
            .CountAsync(
                o => o.EmployeeId == theEmployeeId &&
                     (o.OrderStatus == (int)OrderStatus.Confirmed || o.OrderStatus == (int)OrderStatus.Shipped),
                theCancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, int>> CountSalesOrdersByEmployeesAsync(
        IReadOnlyCollection<Guid> theEmployeeIds,
        CancellationToken theCancellationToken = default)
    {
        if (theEmployeeIds.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        var aRows = await myAppDbContext.Orders.AsNoTracking()
            .Where(o => theEmployeeIds.Contains(o.EmployeeId) &&
                        (o.OrderStatus == (int)OrderStatus.Confirmed || o.OrderStatus == (int)OrderStatus.Shipped))
            .GroupBy(o => o.EmployeeId)
            .Select(g => new { EmployeeId = g.Key, Count = g.Count() })
            .ToListAsync(theCancellationToken);

        return aRows.ToDictionary(r => r.EmployeeId, r => r.Count);
    }

    public async Task<IReadOnlyList<CustomerOrderTotal>> GetCustomerOrderTotalsAsync(CancellationToken theCancellationToken = default)
    {
        var aRows = await myAppDbContext.Orders.AsNoTracking()
            .Where(o => o.OrderStatus == (int)OrderStatus.Confirmed || o.OrderStatus == (int)OrderStatus.Shipped)
            .GroupBy(o => o.CustomerId)
            .Select(g => new
            {
                CustomerId = g.Key,
                TotalOrders = g.Count(),
                TotalSpent = g.Sum(o => o.TotalAmount),
                Currency = g.Max(o => o.TotalCurrency)
            })
            .ToListAsync(theCancellationToken);

        return aRows
            .Select(r => new CustomerOrderTotal(r.CustomerId, r.TotalOrders, r.TotalSpent, r.Currency ?? "VND"))
            .ToList();
    }
}
