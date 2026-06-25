using Microsoft.EntityFrameworkCore;
using SellingNewProduct.Domain.Categories;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;
using SellingNewProduct.Domain.Interfaces.Outbound;
using SellingNewProduct.Infrastructure.MongoDB.Saga.Mapping;
using SellingNewProduct.Infrastructure.MongoDB.Saga.Models;
using SellingNewProduct.Infrastructure.MongoDB.Saga.Persistence;

namespace SellingNewProduct.Infrastructure.MongoDB.Saga.Repositories.Read;

/// <summary>
/// MongoDB read side for categories. Category and product both live in MongoDB here, so the
/// per-category product count/stock value is computed by loading the products once and grouping
/// in memory. Soft-deleted rows are excluded explicitly.
/// </summary>
internal sealed class MongoCategoryReadRepository : ICategoryReadRepository
{
    private const int DeletedStatus = (int)EntityStatus.Deleted;

    private readonly MongoReadDbContext myMongoReadDbContext;

    public MongoCategoryReadRepository(MongoReadDbContext theMongoReadDbContext)
    {
        myMongoReadDbContext = theMongoReadDbContext;
    }

    public async Task<Category?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default)
    {
        var aDocument = await myMongoReadDbContext.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == theId && r.Status != DeletedStatus, theCancellationToken);

        return aDocument is null ? null : CategoryMapper.ToDomain(aDocument);
    }

    public async Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken theCancellationToken = default)
    {
        var aDocuments = await myMongoReadDbContext.Categories
            .AsNoTracking()
            .Where(r => r.Status != DeletedStatus)
            .ToListAsync(theCancellationToken);

        return aDocuments.Select(CategoryMapper.ToDomain).ToList();
    }

    public async Task<IReadOnlyList<CategorySummaryView>> GetCategorySummariesAsync(CancellationToken theCancellationToken = default)
    {
        var aCategories = await myMongoReadDbContext.Categories.AsNoTracking()
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

        var aCategories = await myMongoReadDbContext.Categories.AsNoTracking()
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

    private async Task<Dictionary<Guid, (int Count, decimal StockValue)>> BuildProductStatsAsync(CancellationToken theCancellationToken)
    {
        var aProducts = await myMongoReadDbContext.Products.AsNoTracking()
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
