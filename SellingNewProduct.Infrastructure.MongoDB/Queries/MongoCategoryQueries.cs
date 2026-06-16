using Microsoft.EntityFrameworkCore;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Abstractions;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;
using SellingNewProduct.Infrastructure.MongoDB.Models;
using SellingNewProduct.Infrastructure.MongoDB.Persistence;

namespace SellingNewProduct.Infrastructure.MongoDB.Queries;

/// <summary>
/// MongoDB read side for categories. With no JOIN, the products are loaded once and
/// grouped per category in memory to compute the product count and stock value. Soft-deleted
/// categories and products are excluded explicitly.
/// </summary>
internal sealed class MongoCategoryQueries : ICategoryQueries
{
    private const int DeletedStatus = (int)EntityStatus.Deleted;

    private readonly MongoAppDbContext myMongoAppDbContext;

    public MongoCategoryQueries(MongoAppDbContext theMongoAppDbContext)
    {
        myMongoAppDbContext = theMongoAppDbContext;
    }

    public async Task<IReadOnlyList<CategorySummaryView>> GetCategorySummariesAsync(CancellationToken theCancellationToken = default)
    {
        var aCategories = await myMongoAppDbContext.Categories.AsNoTracking()
            .Where(c => c.Status != DeletedStatus)
            .ToListAsync(theCancellationToken);

        var aStats = await BuildProductStatsAsync(theCancellationToken);

        return aCategories
            .OrderBy(c => c.Name)
            .Select(c => ToSummary(c, aStats))
            .ToList();
    }

    public async Task<PagedResult<CategorySummaryView>> SearchAsync(
        CategorySearchQuery theQuery,
        CancellationToken theCancellationToken = default)
    {
        var aPage = new PageRequest(theQuery.Page, theQuery.PageSize);

        var aCategories = await myMongoAppDbContext.Categories.AsNoTracking()
            .Where(c => c.Status != DeletedStatus)
            .ToListAsync(theCancellationToken);

        IEnumerable<CategoryDocument> aFiltered = aCategories;

        if (!string.IsNullOrWhiteSpace(theQuery.Name))
        {
            var aName = theQuery.Name.Trim();
            aFiltered = aFiltered.Where(c => c.Name.Contains(aName, StringComparison.OrdinalIgnoreCase));
        }

        aFiltered = theQuery.SortDescending ? aFiltered.OrderByDescending(c => c.Name) : aFiltered.OrderBy(c => c.Name);

        var aMatched = aFiltered.ToList();

        var aStats = await BuildProductStatsAsync(theCancellationToken);

        var aItems = aMatched
            .Skip(aPage.Skip)
            .Take(aPage.PageSize)
            .Select(c => ToSummary(c, aStats))
            .ToList();

        return new PagedResult<CategorySummaryView>(aItems, aPage.Page, aPage.PageSize, aMatched.Count);
    }

    // Per-category product count and total stock value, computed once from all live products.
    private async Task<Dictionary<Guid, (int Count, decimal StockValue)>> BuildProductStatsAsync(CancellationToken theCancellationToken)
    {
        var aProducts = await myMongoAppDbContext.Products.AsNoTracking()
            .Where(p => p.Status != DeletedStatus)
            .ToListAsync(theCancellationToken);

        return aProducts
            .GroupBy(p => p.CategoryId)
            .ToDictionary(g => g.Key, g => (g.Count(), g.Sum(p => p.PriceAmount * p.StockQuantity)));
    }

    private static CategorySummaryView ToSummary(CategoryDocument theDoc, Dictionary<Guid, (int Count, decimal StockValue)> theStats)
    {
        var aStat = theStats.TryGetValue(theDoc.Id, out var aValue) ? aValue : (Count: 0, StockValue: 0m);
        return new CategorySummaryView(
            theDoc.Id,
            theDoc.Name,
            theDoc.Description,
            aStat.Count,
            aStat.StockValue,
            ((EntityStatus)theDoc.Status).ToString());
    }
}
