using Microsoft.EntityFrameworkCore;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Abstractions;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;
using SellingNewProduct.Infrastructure.SqlServer.Persistence;

namespace SellingNewProduct.Infrastructure.SqlServer.Queries;

/// <summary>
/// SQL Server read side for the product catalogue. Filters, sorting and paging are
/// all pushed down to the database (WHERE / ORDER BY / OFFSET-FETCH) and the category
/// name is brought in with a JOIN — a single SQL statement, no rows loaded just to be
/// thrown away. Soft-deleted rows are excluded by the Global Query Filter.
/// </summary>
internal sealed class SqlServerProductQueries : IProductQueries
{
    private readonly AppDbContext myAppDbContext;

    public SqlServerProductQueries(AppDbContext theAppDbContext)
    {
        myAppDbContext = theAppDbContext;
    }

    public async Task<ProductSummaryView?> GetByIdAsync(Guid theProductId, CancellationToken theCancellationToken = default)
    {
        // Same JOIN as the search, narrowed to one product.
        var aRow = await (
            from p in myAppDbContext.Products.AsNoTracking()
            join c in myAppDbContext.Categories on p.CategoryId equals c.Id
            where p.Id == theProductId
            select new
            {
                p.Id,
                p.Name,
                p.Sku,
                p.Color,
                p.Size,
                p.PriceAmount,
                p.PriceCurrency,
                p.StockQuantity,
                p.CategoryId,
                CategoryName = c.Name,
                p.Status
            }).FirstOrDefaultAsync(theCancellationToken);

        return aRow is null
            ? null
            : new ProductSummaryView(
                aRow.Id,
                aRow.Name,
                aRow.Sku,
                aRow.Color,
                aRow.Size,
                aRow.PriceAmount,
                aRow.PriceCurrency,
                aRow.StockQuantity,
                aRow.CategoryId,
                aRow.CategoryName,
                ((EntityStatus)aRow.Status).ToString());
    }

    public async Task<PagedResult<ProductSummaryView>> SearchAsync(
        ProductSearchQuery theQuery,
        CancellationToken theCancellationToken = default)
    {
        var aPage = new PageRequest(theQuery.Page, theQuery.PageSize);

        // JOIN Products x Categories so each row carries the category name (loại hàng).
        var aQuery =
            from p in myAppDbContext.Products.AsNoTracking()
            join c in myAppDbContext.Categories on p.CategoryId equals c.Id
            select new { p, CategoryName = c.Name };

        if (!string.IsNullOrWhiteSpace(theQuery.Name))
        {
            var aName = theQuery.Name.Trim();
            aQuery = aQuery.Where(x => x.p.Name.Contains(aName));
        }

        if (theQuery.CategoryId is not null)
        {
            aQuery = aQuery.Where(x => x.p.CategoryId == theQuery.CategoryId);
        }

        if (theQuery.PriceFrom is not null)
        {
            aQuery = aQuery.Where(x => x.p.PriceAmount >= theQuery.PriceFrom);
        }

        if (theQuery.PriceTo is not null)
        {
            aQuery = aQuery.Where(x => x.p.PriceAmount <= theQuery.PriceTo);
        }

        if (theQuery.MinStock is not null)
        {
            aQuery = aQuery.Where(x => x.p.StockQuantity >= theQuery.MinStock);
        }

        if (theQuery.MaxStock is not null)
        {
            aQuery = aQuery.Where(x => x.p.StockQuantity <= theQuery.MaxStock);
        }

        if (theQuery.Status is not null)
        {
            var aStatusValue = (int)theQuery.Status.Value;
            aQuery = aQuery.Where(x => x.p.Status == aStatusValue);
        }

        // Map the requested column name to an ORDER BY (default: name). Whitelisting the
        // column keeps the contract storage-agnostic and avoids any "sort by arbitrary
        // string" surprises.
        aQuery = (theQuery.SortBy?.Trim().ToLowerInvariant()) switch
        {
            "price" => theQuery.SortDescending ? aQuery.OrderByDescending(x => x.p.PriceAmount) : aQuery.OrderBy(x => x.p.PriceAmount),
            "stock" => theQuery.SortDescending ? aQuery.OrderByDescending(x => x.p.StockQuantity) : aQuery.OrderBy(x => x.p.StockQuantity),
            _ => theQuery.SortDescending ? aQuery.OrderByDescending(x => x.p.Name) : aQuery.OrderBy(x => x.p.Name)
        };

        var aTotalCount = await aQuery.CountAsync(theCancellationToken);

        var aRows = await aQuery
            .Skip(aPage.Skip)
            .Take(aPage.PageSize)
            .Select(x => new
            {
                x.p.Id,
                x.p.Name,
                x.p.Sku,
                x.p.Color,
                x.p.Size,
                x.p.PriceAmount,
                x.p.PriceCurrency,
                x.p.StockQuantity,
                x.p.CategoryId,
                x.CategoryName,
                x.p.Status
            })
            .ToListAsync(theCancellationToken);

        var aItems = aRows
            .Select(r => new ProductSummaryView(
                r.Id,
                r.Name,
                r.Sku,
                r.Color,
                r.Size,
                r.PriceAmount,
                r.PriceCurrency,
                r.StockQuantity,
                r.CategoryId,
                r.CategoryName,
                ((EntityStatus)r.Status).ToString()))
            .ToList();

        return new PagedResult<ProductSummaryView>(aItems, aPage.Page, aPage.PageSize, aTotalCount);
    }
}
