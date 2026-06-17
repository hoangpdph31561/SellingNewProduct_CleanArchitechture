using Microsoft.EntityFrameworkCore;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.ReadModels;
using SellingNewProduct.Domain.Orders;
using SellingNewProduct.Domain.Repositories;
using SellingNewProduct.Infrastructure.SqlServer.Persistence;

namespace SellingNewProduct.Infrastructure.SqlServer.Repositories;

/// <summary>
/// SQL Server reporting: JOIN several tables + GROUP BY, aggregated on the database.
/// This is where JOIN pays off the most — the aggregation is pushed down to the
/// database instead of pulling thousands of rows into the app to sum them.
/// </summary>
internal sealed class SqlServerReportRepository : IReportRepository
{
    private readonly AppDbContext myAppDbContext;

    public SqlServerReportRepository(AppDbContext theAppDbContext)
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

    public async Task<IReadOnlyList<CategorySalesView>> GetSalesByCategoryAsync(CancellationToken theCancellationToken = default)
    {
        // OrderDetails x Orders x Products x Categories, GROUP BY category. Real sales only.
        var aQuery =
            from d in myAppDbContext.OrderDetails.AsNoTracking()
            join o in myAppDbContext.Orders on d.OrderId equals o.Id
            join p in myAppDbContext.Products on d.ProductId equals p.Id
            join c in myAppDbContext.Categories on p.CategoryId equals c.Id
            where o.OrderStatus == (int)OrderStatus.Confirmed || o.OrderStatus == (int)OrderStatus.Shipped
            group d by new { c.Id, CategoryName = c.Name } into g
            select new CategorySalesView(
                g.Key.Id,
                g.Key.CategoryName,
                g.Sum(x => x.Quantity),
                g.Sum(x => x.UnitPriceAmount * x.Quantity));

        return await aQuery
            .OrderByDescending(v => v.TotalRevenue)
            .ToListAsync(theCancellationToken);
    }

    public async Task<IReadOnlyList<DailySalesView>> GetDailySalesAsync(
        DateTime? theFromUtc = null,
        DateTime? theToUtc = null,
        CancellationToken theCancellationToken = default)
    {
        var aQuery = myAppDbContext.Orders.AsNoTracking()
            .Where(o => o.OrderStatus == (int)OrderStatus.Confirmed || o.OrderStatus == (int)OrderStatus.Shipped);

        if (theFromUtc is not null)
        {
            aQuery = aQuery.Where(o => o.OrderDate >= theFromUtc);
        }

        if (theToUtc is not null)
        {
            aQuery = aQuery.Where(o => o.OrderDate <= theToUtc);
        }

        // GROUP BY the DATE part of OrderDate (EF translates .Date to CAST(... AS date)).
        return await aQuery
            .GroupBy(o => o.OrderDate.Date)
            .Select(g => new DailySalesView(g.Key, g.Count(), g.Sum(o => o.TotalAmount)))
            .OrderBy(v => v.Date)
            .ToListAsync(theCancellationToken);
    }

    public async Task<PagedResult<LowStockProductView>> GetLowStockProductsAsync(
        int theThreshold = 5,
        int thePage = 1,
        int thePageSize = PageRequest.DefaultPageSize,
        CancellationToken theCancellationToken = default)
    {
        var aPage = new PageRequest(thePage, thePageSize);

        // Products x Categories, keeping only stock at or below the threshold.
        var aQuery =
            from p in myAppDbContext.Products.AsNoTracking()
            join c in myAppDbContext.Categories on p.CategoryId equals c.Id
            where p.StockQuantity <= theThreshold
            select new LowStockProductView(p.Id, p.Name, c.Name, p.StockQuantity);

        var aTotalCount = await aQuery.CountAsync(theCancellationToken);

        var aItems = await aQuery
            .OrderBy(v => v.StockQuantity)
            .Skip(aPage.Skip)
            .Take(aPage.PageSize)
            .ToListAsync(theCancellationToken);

        return new PagedResult<LowStockProductView>(aItems, aPage.Page, aPage.PageSize, aTotalCount);
    }
}
