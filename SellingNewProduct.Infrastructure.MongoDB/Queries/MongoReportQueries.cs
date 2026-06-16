using Microsoft.EntityFrameworkCore;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Abstractions;
using SellingNewProduct.Domain.ReadModels;
using SellingNewProduct.Domain.Orders;
using SellingNewProduct.Infrastructure.MongoDB.Persistence;

namespace SellingNewProduct.Infrastructure.MongoDB.Queries;

/// <summary>
/// MongoDB reporting. Since there is no relational JOIN/GROUP BY, we load the
/// related collections and aggregate them with LINQ-to-objects. For large data the
/// more "Mongo-native" approach is an aggregation pipeline ($lookup, $group) or
/// denormalised data — but the version below is clear enough for learning and keeps
/// the same <see cref="IReportQueries"/> contract.
/// </summary>
internal sealed class MongoReportQueries : IReportQueries
{
    private const int DeletedStatus = (int)EntityStatus.Deleted;

    private readonly MongoAppDbContext myMongoAppDbContext;

    public MongoReportQueries(MongoAppDbContext theMongoAppDbContext)
    {
        myMongoAppDbContext = theMongoAppDbContext;
    }

    public async Task<PagedResult<BestSellingProductView>> GetBestSellingProductsAsync(
        int thePage = 1,
        int thePageSize = 10,
        CancellationToken theCancellationToken = default)
    {
        var aPage = new PageRequest(thePage, thePageSize);

        // Count only real sales (Confirmed/Shipped). Details are embedded in the order.
        var aOrders = await myMongoAppDbContext.Orders.AsNoTracking()
            .Where(o => o.Status != DeletedStatus &&
                        (o.OrderStatus == (int)OrderStatus.Confirmed || o.OrderStatus == (int)OrderStatus.Shipped))
            .ToListAsync(theCancellationToken);

        var aProducts = await myMongoAppDbContext.Products.AsNoTracking()
            .ToListAsync(theCancellationToken);
        var aCategories = await myMongoAppDbContext.Categories.AsNoTracking()
            .ToListAsync(theCancellationToken);

        var aCategoryNameById = aCategories.ToDictionary(c => c.Id, c => c.Name);
        var aProductById = aProducts.ToDictionary(p => p.Id);

        // Group the order lines (flattened from embedded Details) by product — the GROUP BY equivalent.
        var aRanked = aOrders
            .SelectMany(o => o.Details)
            .GroupBy(d => d.ProductId)
            .Select(g =>
            {
                aProductById.TryGetValue(g.Key, out var aProduct);
                var aCategoryName = aProduct is not null && aCategoryNameById.TryGetValue(aProduct.CategoryId, out var aCat)
                    ? aCat
                    : "(unknown)";

                return new BestSellingProductView(
                    g.Key,
                    aProduct?.Name ?? "(unknown)",
                    aCategoryName,
                    g.Sum(d => d.Quantity),
                    g.Sum(d => d.UnitPriceAmount * d.Quantity));
            })
            .OrderByDescending(v => v.TotalQuantitySold)
            .ToList();

        // Mongo grouping happens in memory, so total = the full ranked list; then take one page.
        var aItems = aRanked
            .Skip(aPage.Skip)
            .Take(aPage.PageSize)
            .ToList();

        return new PagedResult<BestSellingProductView>(aItems, aPage.Page, aPage.PageSize, aRanked.Count);
    }

    public async Task<IReadOnlyList<EmployeeSalesView>> GetEmployeeSalesLeaderboardAsync(CancellationToken theCancellationToken = default)
    {
        var aOrders = await myMongoAppDbContext.Orders.AsNoTracking()
            .Where(o => o.Status != DeletedStatus &&
                        (o.OrderStatus == (int)OrderStatus.Confirmed || o.OrderStatus == (int)OrderStatus.Shipped))
            .ToListAsync(theCancellationToken);

        var aEmployees = await myMongoAppDbContext.Employees.AsNoTracking()
            .ToListAsync(theCancellationToken);
        var aEmployeeById = aEmployees.ToDictionary(e => e.Id);

        return aOrders
            .GroupBy(o => o.EmployeeId)
            .Select(g =>
            {
                aEmployeeById.TryGetValue(g.Key, out var aEmployee);
                return new EmployeeSalesView(
                    g.Key,
                    aEmployee?.FullName ?? "(unknown)",
                    aEmployee?.Position ?? "(unknown)",
                    g.Count(),
                    g.Sum(o => o.TotalAmount));
            })
            .OrderByDescending(v => v.TotalRevenue)
            .ToList();
    }

    public async Task<IReadOnlyList<CategorySalesView>> GetSalesByCategoryAsync(CancellationToken theCancellationToken = default)
    {
        var aOrders = await myMongoAppDbContext.Orders.AsNoTracking()
            .Where(o => o.Status != DeletedStatus &&
                        (o.OrderStatus == (int)OrderStatus.Confirmed || o.OrderStatus == (int)OrderStatus.Shipped))
            .ToListAsync(theCancellationToken);

        var aProducts = await myMongoAppDbContext.Products.AsNoTracking().ToListAsync(theCancellationToken);
        var aCategories = await myMongoAppDbContext.Categories.AsNoTracking().ToListAsync(theCancellationToken);

        var aProductById = aProducts.ToDictionary(p => p.Id);
        var aCategoryById = aCategories.ToDictionary(c => c.Id);

        // Flatten the embedded order lines, resolve each to its product's category, then group.
        return aOrders
            .SelectMany(o => o.Details)
            .Select(d =>
            {
                aProductById.TryGetValue(d.ProductId, out var aProduct);
                return new
                {
                    CategoryId = aProduct?.CategoryId ?? Guid.Empty,
                    d.Quantity,
                    Revenue = d.UnitPriceAmount * d.Quantity
                };
            })
            .GroupBy(x => x.CategoryId)
            .Select(g => new CategorySalesView(
                g.Key,
                aCategoryById.TryGetValue(g.Key, out var aCat) ? aCat.Name : "(unknown)",
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
        var aQuery = myMongoAppDbContext.Orders.AsNoTracking()
            .Where(o => o.Status != DeletedStatus &&
                        (o.OrderStatus == (int)OrderStatus.Confirmed || o.OrderStatus == (int)OrderStatus.Shipped));

        if (theFromUtc is not null)
        {
            aQuery = aQuery.Where(o => o.OrderDate >= theFromUtc);
        }

        if (theToUtc is not null)
        {
            aQuery = aQuery.Where(o => o.OrderDate <= theToUtc);
        }

        var aOrders = await aQuery.ToListAsync(theCancellationToken);

        // Group by the date part in memory (Mongo has no GROUP BY on a computed date here).
        return aOrders
            .GroupBy(o => o.OrderDate.Date)
            .Select(g => new DailySalesView(g.Key, g.Count(), g.Sum(o => o.TotalAmount)))
            .OrderBy(v => v.Date)
            .ToList();
    }

    public async Task<PagedResult<LowStockProductView>> GetLowStockProductsAsync(
        int theThreshold = 5,
        int thePage = 1,
        int thePageSize = PageRequest.DefaultPageSize,
        CancellationToken theCancellationToken = default)
    {
        var aPage = new PageRequest(thePage, thePageSize);

        // The stock filter is a simple comparison, so it runs on the database.
        var aProducts = await myMongoAppDbContext.Products.AsNoTracking()
            .Where(p => p.Status != DeletedStatus && p.StockQuantity <= theThreshold)
            .ToListAsync(theCancellationToken);

        var aSorted = aProducts.OrderBy(p => p.StockQuantity).ToList();
        var aTotalCount = aSorted.Count;

        var aPageDocs = aSorted
            .Skip(aPage.Skip)
            .Take(aPage.PageSize)
            .ToList();

        // Stitch the category name for this page's products only.
        var aCategoryIds = aPageDocs.Select(p => p.CategoryId).Distinct().ToList();
        var aCategoryNameById = (await myMongoAppDbContext.Categories.AsNoTracking()
            .Where(c => aCategoryIds.Contains(c.Id))
            .ToListAsync(theCancellationToken))
            .ToDictionary(c => c.Id, c => c.Name);

        var aItems = aPageDocs
            .Select(p => new LowStockProductView(
                p.Id,
                p.Name,
                aCategoryNameById.TryGetValue(p.CategoryId, out var aName) ? aName : "(unknown)",
                p.StockQuantity))
            .ToList();

        return new PagedResult<LowStockProductView>(aItems, aPage.Page, aPage.PageSize, aTotalCount);
    }
}
