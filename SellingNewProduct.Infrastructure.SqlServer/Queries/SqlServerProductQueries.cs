using Microsoft.EntityFrameworkCore;
using SellingNewProduct.Application.Common;
using SellingNewProduct.Application.Queries;
using SellingNewProduct.Application.ReadModels;
using SellingNewProduct.Domain.Common;
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

    public async Task<PagedResult<ProductSummaryView>> SearchAsync(
        string? theName = null,
        Guid? theCategoryId = null,
        decimal? thePriceFrom = null,
        decimal? thePriceTo = null,
        int? theMinStock = null,
        int? theMaxStock = null,
        EntityStatus? theStatus = null,
        int thePage = 1,
        int thePageSize = PageRequest.DefaultPageSize,
        string? theSortBy = null,
        bool theSortDescending = false,
        CancellationToken theCancellationToken = default)
    {
        var aPage = new PageRequest(thePage, thePageSize);

        // JOIN Products x Categories so each row carries the category name (loại hàng).
        var aQuery =
            from p in myAppDbContext.Products.AsNoTracking()
            join c in myAppDbContext.Categories on p.CategoryId equals c.Id
            select new { p, CategoryName = c.Name };

        if (!string.IsNullOrWhiteSpace(theName))
        {
            var aName = theName.Trim();
            aQuery = aQuery.Where(x => x.p.Name.Contains(aName));
        }

        if (theCategoryId is not null)
        {
            aQuery = aQuery.Where(x => x.p.CategoryId == theCategoryId);
        }

        if (thePriceFrom is not null)
        {
            aQuery = aQuery.Where(x => x.p.PriceAmount >= thePriceFrom);
        }

        if (thePriceTo is not null)
        {
            aQuery = aQuery.Where(x => x.p.PriceAmount <= thePriceTo);
        }

        if (theMinStock is not null)
        {
            aQuery = aQuery.Where(x => x.p.StockQuantity >= theMinStock);
        }

        if (theMaxStock is not null)
        {
            aQuery = aQuery.Where(x => x.p.StockQuantity <= theMaxStock);
        }

        if (theStatus is not null)
        {
            var aStatusValue = (int)theStatus.Value;
            aQuery = aQuery.Where(x => x.p.Status == aStatusValue);
        }

        // Map the requested column name to an ORDER BY (default: name). Whitelisting the
        // column keeps the contract storage-agnostic and avoids any "sort by arbitrary
        // string" surprises.
        aQuery = (theSortBy?.Trim().ToLowerInvariant()) switch
        {
            "price" => theSortDescending ? aQuery.OrderByDescending(x => x.p.PriceAmount) : aQuery.OrderBy(x => x.p.PriceAmount),
            "stock" => theSortDescending ? aQuery.OrderByDescending(x => x.p.StockQuantity) : aQuery.OrderBy(x => x.p.StockQuantity),
            _ => theSortDescending ? aQuery.OrderByDescending(x => x.p.Name) : aQuery.OrderBy(x => x.p.Name)
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
