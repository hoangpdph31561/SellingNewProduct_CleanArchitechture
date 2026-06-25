using Microsoft.EntityFrameworkCore;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Orders;
using SellingNewProduct.Domain.ReadModels;
using SellingNewProduct.Domain.Interfaces.Outbound;
using SellingNewProduct.Infrastructure.Saga.Core.CrossDb;
using SellingNewProduct.Infrastructure.SqlServer.Saga.Persistence;

namespace SellingNewProduct.Infrastructure.SqlServer.Saga.Repositories.Read;

/// <summary>
/// Reporting for the saga provider. This is the genuinely hard part of polyglot CQRS: the reports
/// JOIN order data (SQL) with product/category/employee data (MongoDB), and you cannot JOIN across
/// two stores. So the order-side aggregation runs on SQL and the product/category/employee
/// dimensions are fetched from MongoDB (through <see cref="ICrossDbDirectory"/>) and joined in memory.
///
/// In a production system you would instead maintain a denormalised read model (materialised view)
/// kept up to date by events. The in-memory join here keeps the learning project simple and honest
/// about the trade-off.
/// </summary>
internal sealed class SqlReportReadRepository : IReportReadRepository
{
    private readonly AppDbContext myAppDbContext;
    private readonly ICrossDbDirectory myDirectory;

    public SqlReportReadRepository(AppDbContext theAppDbContext, ICrossDbDirectory theDirectory)
    {
        myAppDbContext = theAppDbContext;
        myDirectory = theDirectory;
    }

    public async Task<PagedResult<BestSellingProductView>> GetBestSellingProductsAsync(
        int thePage = 1,
        int thePageSize = 10,
        CancellationToken theCancellationToken = default)
    {
        var aPage = new PageRequest(thePage, thePageSize);

        var aLines = await SalesLinesQuery()
            .Select(d => new { d.ProductId, d.ProductName, d.Quantity, Revenue = d.UnitPriceAmount * d.Quantity })
            .ToListAsync(theCancellationToken);

        var aCategoryNameByProductId = await GetCategoryNameByProductIdAsync(theCancellationToken);

        var aGrouped = aLines
            .GroupBy(l => l.ProductId)
            .Select(g => new BestSellingProductView(
                g.Key,
                g.First().ProductName,
                aCategoryNameByProductId.GetValueOrDefault(g.Key, string.Empty),
                g.Sum(x => x.Quantity),
                g.Sum(x => x.Revenue)))
            .OrderByDescending(v => v.TotalQuantitySold)
            .ToList();

        var aItems = aGrouped.Skip(aPage.Skip).Take(aPage.PageSize).ToList();
        return new PagedResult<BestSellingProductView>(aItems, aPage.Page, aPage.PageSize, aGrouped.Count);
    }

    public async Task<IReadOnlyList<EmployeeSalesView>> GetEmployeeSalesLeaderboardAsync(CancellationToken theCancellationToken = default)
    {
        var aRows = await myAppDbContext.Orders.AsNoTracking()
            .Where(o => o.OrderStatus == (int)OrderStatus.Confirmed || o.OrderStatus == (int)OrderStatus.Shipped)
            .GroupBy(o => o.EmployeeId)
            .Select(g => new { EmployeeId = g.Key, Count = g.Count(), Total = g.Sum(o => o.TotalAmount) })
            .ToListAsync(theCancellationToken);

        var aEmployees = await myDirectory.GetEmployeesAsync(theCancellationToken);
        var aEmployeeById = aEmployees.ToDictionary(e => e.Id);

        return aRows
            .Select(r =>
            {
                aEmployeeById.TryGetValue(r.EmployeeId, out var aEmployee);
                return new EmployeeSalesView(
                    r.EmployeeId,
                    aEmployee?.Name ?? string.Empty,
                    aEmployee?.Position ?? string.Empty,
                    r.Count,
                    r.Total);
            })
            .OrderByDescending(v => v.TotalRevenue)
            .ToList();
    }

    public async Task<IReadOnlyList<CategorySalesView>> GetSalesByCategoryAsync(CancellationToken theCancellationToken = default)
    {
        var aLines = await SalesLinesQuery()
            .Select(d => new { d.ProductId, d.Quantity, Revenue = d.UnitPriceAmount * d.Quantity })
            .ToListAsync(theCancellationToken);

        var aProducts = await myDirectory.GetProductsAsync(theCancellationToken);
        var aCategoryIdByProductId = aProducts.ToDictionary(p => p.Id, p => p.CategoryId);
        var aCategoryNameById = await myDirectory.GetCategoryNamesAsync(theCancellationToken);

        return aLines
            .GroupBy(l => aCategoryIdByProductId.GetValueOrDefault(l.ProductId))
            .Where(g => g.Key != Guid.Empty)
            .Select(g => new CategorySalesView(
                g.Key,
                aCategoryNameById.GetValueOrDefault(g.Key, string.Empty),
                g.Sum(x => x.Quantity),
                g.Sum(x => x.Revenue)))
            .OrderByDescending(v => v.TotalRevenue)
            .ToList();
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

        // Pure MongoDB report: products + their category names, both from the catalogue store.
        var aProducts = await myDirectory.GetProductsAsync(theCancellationToken);
        var aCategoryNameById = await myDirectory.GetCategoryNamesAsync(theCancellationToken);

        var aLowStock = aProducts
            .Where(p => p.StockQuantity <= theThreshold)
            .OrderBy(p => p.StockQuantity)
            .Select(p => new LowStockProductView(
                p.Id,
                p.Name,
                aCategoryNameById.GetValueOrDefault(p.CategoryId, string.Empty),
                p.StockQuantity))
            .ToList();

        var aItems = aLowStock.Skip(aPage.Skip).Take(aPage.PageSize).ToList();
        return new PagedResult<LowStockProductView>(aItems, aPage.Page, aPage.PageSize, aLowStock.Count);
    }

    private IQueryable<Models.OrderDetailRecord> SalesLinesQuery()
    {
        return from d in myAppDbContext.OrderDetails.AsNoTracking()
               join o in myAppDbContext.Orders.AsNoTracking() on d.OrderId equals o.Id
               where o.OrderStatus == (int)OrderStatus.Confirmed || o.OrderStatus == (int)OrderStatus.Shipped
               select d;
    }

    private async Task<Dictionary<Guid, string>> GetCategoryNameByProductIdAsync(CancellationToken theCancellationToken)
    {
        var aProducts = await myDirectory.GetProductsAsync(theCancellationToken);
        var aCategoryNameById = await myDirectory.GetCategoryNamesAsync(theCancellationToken);

        return aProducts.ToDictionary(
            p => p.Id,
            p => aCategoryNameById.GetValueOrDefault(p.CategoryId, string.Empty));
    }
}
