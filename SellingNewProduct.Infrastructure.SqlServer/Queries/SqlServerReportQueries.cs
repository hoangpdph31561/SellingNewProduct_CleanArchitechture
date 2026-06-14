using Microsoft.EntityFrameworkCore;
using SellingNewProduct.Application.Common;
using SellingNewProduct.Application.Queries;
using SellingNewProduct.Application.ReadModels;
using SellingNewProduct.Domain.Orders;
using SellingNewProduct.Infrastructure.SqlServer.Persistence;

namespace SellingNewProduct.Infrastructure.SqlServer.Queries;

/// <summary>
/// SQL Server reporting: JOIN several tables + GROUP BY, aggregated on the database.
/// This is where JOIN pays off the most — the aggregation is pushed down to the
/// database instead of pulling thousands of rows into the app to sum them.
/// </summary>
internal sealed class SqlServerReportQueries : IReportQueries
{
    private readonly AppDbContext myAppDbContext;

    public SqlServerReportQueries(AppDbContext theAppDbContext)
    {
        myAppDbContext = theAppDbContext;
    }

    public async Task<PagedResult<BestSellingProductView>> GetBestSellingProductsAsync(
        int thePage = 1,
        int thePageSize = 10,
        CancellationToken theCancellationToken = default)
    {
        var aPage = new PageRequest(thePage, thePageSize);

        // OrderDetails x Products x Categories x Orders, counting only real sales.
        var aQuery =
            from d in myAppDbContext.OrderDetails.AsNoTracking()
            join o in myAppDbContext.Orders on d.OrderId equals o.Id
            join p in myAppDbContext.Products on d.ProductId equals p.Id
            join c in myAppDbContext.Categories on p.CategoryId equals c.Id
            where o.OrderStatus == (int)OrderStatus.Confirmed || o.OrderStatus == (int)OrderStatus.Shipped
            group new { d, c } by new { p.Id, ProductName = p.Name, CategoryName = c.Name } into g
            select new BestSellingProductView(
                g.Key.Id,
                g.Key.ProductName,
                g.Key.CategoryName,
                g.Sum(x => x.d.Quantity),
                g.Sum(x => x.d.UnitPriceAmount * x.d.Quantity));

        // Total = number of distinct products sold (i.e. number of GROUP BY buckets).
        var aTotalCount = await aQuery.CountAsync(theCancellationToken);

        var aItems = await aQuery
            .OrderByDescending(v => v.TotalQuantitySold)
            .Skip(aPage.Skip)
            .Take(aPage.PageSize)
            .ToListAsync(theCancellationToken);

        return new PagedResult<BestSellingProductView>(aItems, aPage.Page, aPage.PageSize, aTotalCount);
    }

    public async Task<IReadOnlyList<EmployeeSalesView>> GetEmployeeSalesLeaderboardAsync(CancellationToken theCancellationToken = default)
    {
        // Orders x Employees, GROUP BY employee. Only Confirmed/Shipped orders count.
        var aQuery =
            from o in myAppDbContext.Orders.AsNoTracking()
            join e in myAppDbContext.Employees on o.EmployeeId equals e.Id
            where o.OrderStatus == (int)OrderStatus.Confirmed || o.OrderStatus == (int)OrderStatus.Shipped
            group o by new { e.Id, e.FullName, e.Position } into g
            select new EmployeeSalesView(
                g.Key.Id,
                g.Key.FullName,
                g.Key.Position,
                g.Count(),
                g.Sum(x => x.TotalAmount));

        return await aQuery
            .OrderByDescending(v => v.TotalRevenue)
            .ToListAsync(theCancellationToken);
    }
}
