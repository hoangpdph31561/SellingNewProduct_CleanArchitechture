using Microsoft.EntityFrameworkCore;
using SellingNewProduct.Application.Common;
using SellingNewProduct.Application.Queries;
using SellingNewProduct.Application.ReadModels;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Infrastructure.MongoDB.Models;
using SellingNewProduct.Infrastructure.MongoDB.Persistence;

namespace SellingNewProduct.Infrastructure.MongoDB.Queries;

/// <summary>
/// MongoDB read side for the product catalogue. The simple field comparisons
/// (category, price range, stock range, status) are pushed to the database. The
/// text search on the name, the sorting and the paging are then done in memory —
/// this is the documented Mongo tradeoff in this project: no JOIN and limited text
/// translation, so part of the work happens at the application layer. The contract
/// (<see cref="IProductQueries"/>) is identical to the SQL version; only the
/// execution strategy differs. Mongo has no Global Query Filter, so we exclude
/// soft-deleted rows ourselves.
/// </summary>
internal sealed class MongoProductQueries : IProductQueries
{
    private const int DeletedStatus = (int)EntityStatus.Deleted;

    private readonly MongoAppDbContext myMongoAppDbContext;

    public MongoProductQueries(MongoAppDbContext theMongoAppDbContext)
    {
        myMongoAppDbContext = theMongoAppDbContext;
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

        // Push the field comparisons to the database to shrink the candidate set.
        var aQuery = myMongoAppDbContext.Products.AsNoTracking()
            .Where(p => p.Status != DeletedStatus);

        if (theCategoryId is not null)
        {
            aQuery = aQuery.Where(p => p.CategoryId == theCategoryId);
        }

        if (thePriceFrom is not null)
        {
            aQuery = aQuery.Where(p => p.PriceAmount >= thePriceFrom);
        }

        if (thePriceTo is not null)
        {
            aQuery = aQuery.Where(p => p.PriceAmount <= thePriceTo);
        }

        if (theMinStock is not null)
        {
            aQuery = aQuery.Where(p => p.StockQuantity >= theMinStock);
        }

        if (theMaxStock is not null)
        {
            aQuery = aQuery.Where(p => p.StockQuantity <= theMaxStock);
        }

        if (theStatus is not null)
        {
            var aStatusValue = (int)theStatus.Value;
            aQuery = aQuery.Where(p => p.Status == aStatusValue);
        }

        var aCandidates = await aQuery.ToListAsync(theCancellationToken);

        // Name "contains" + sorting in memory (Mongo has no JOIN/limited text translation).
        IEnumerable<ProductDocument> aFiltered = aCandidates;

        if (!string.IsNullOrWhiteSpace(theName))
        {
            var aName = theName.Trim();
            aFiltered = aFiltered.Where(p => p.Name.Contains(aName, StringComparison.OrdinalIgnoreCase));
        }

        aFiltered = (theSortBy?.Trim().ToLowerInvariant()) switch
        {
            "price" => theSortDescending ? aFiltered.OrderByDescending(p => p.PriceAmount) : aFiltered.OrderBy(p => p.PriceAmount),
            "stock" => theSortDescending ? aFiltered.OrderByDescending(p => p.StockQuantity) : aFiltered.OrderBy(p => p.StockQuantity),
            _ => theSortDescending ? aFiltered.OrderByDescending(p => p.Name) : aFiltered.OrderBy(p => p.Name)
        };

        var aMatched = aFiltered.ToList();
        var aTotalCount = aMatched.Count;

        var aPageDocs = aMatched
            .Skip(aPage.Skip)
            .Take(aPage.PageSize)
            .ToList();

        // Stitch the category name for this page's rows only (the equivalent of the JOIN).
        var aCategoryIds = aPageDocs.Select(p => p.CategoryId).Distinct().ToList();
        var aCategoryNameById = (await myMongoAppDbContext.Categories.AsNoTracking()
            .Where(c => aCategoryIds.Contains(c.Id))
            .ToListAsync(theCancellationToken))
            .ToDictionary(c => c.Id, c => c.Name);

        var aItems = aPageDocs
            .Select(p => new ProductSummaryView(
                p.Id,
                p.Name,
                p.Sku,
                p.Color,
                p.Size,
                p.PriceAmount,
                p.PriceCurrency,
                p.StockQuantity,
                p.CategoryId,
                aCategoryNameById.TryGetValue(p.CategoryId, out var aCatName) ? aCatName : "(unknown)",
                ((EntityStatus)p.Status).ToString()))
            .ToList();

        return new PagedResult<ProductSummaryView>(aItems, aPage.Page, aPage.PageSize, aTotalCount);
    }
}
