using Microsoft.EntityFrameworkCore;
using SellingNewProduct.Domain.Categories;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;
using SellingNewProduct.Domain.Repositories;
using SellingNewProduct.Infrastructure.MongoDB.Mapping;
using SellingNewProduct.Infrastructure.MongoDB.Models;
using SellingNewProduct.Infrastructure.MongoDB.Persistence;

namespace SellingNewProduct.Infrastructure.MongoDB.Repositories;

internal sealed class MongoCategoryRepository : ICategoryRepository
{
    private const int DeletedStatus = (int)EntityStatus.Deleted;

    private readonly MongoAppDbContext myMongoAppDbContext;

    public MongoCategoryRepository(MongoAppDbContext theMongoAppDbContext)
    {
        myMongoAppDbContext = theMongoAppDbContext;
    }

    public async Task<Category?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default)
    {
        var aDocument = await myMongoAppDbContext.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == theId && r.Status != DeletedStatus, theCancellationToken);

        return aDocument is null ? null : CategoryMapper.ToDomain(aDocument);
    }

    public async Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken theCancellationToken = default)
    {
        var aDocuments = await myMongoAppDbContext.Categories
            .AsNoTracking()
            .Where(r => r.Status != DeletedStatus)
            .ToListAsync(theCancellationToken);

        return aDocuments.Select(CategoryMapper.ToDomain).ToList();
    }

    public async Task<bool> ExistsByNameAsync(string theName, CancellationToken theCancellationToken = default)
    {
        // Mongo has no global query filter, so exclude soft-deleted rows here.
        return await myMongoAppDbContext.Categories
            .AsNoTracking()
            .AnyAsync(r => r.Name == theName && r.Status != DeletedStatus, theCancellationToken);
    }

    public async Task AddAsync(Category theCategory, CancellationToken theCancellationToken = default)
    {
        myMongoAppDbContext.Categories.Add(CategoryMapper.ToDocument(theCategory));
        await myMongoAppDbContext.SaveChangesAsync(theCancellationToken);
    }

    public async Task UpdateAsync(Category theCategory, CancellationToken theCancellationToken = default)
    {
        var aDocument = await myMongoAppDbContext.Categories.FirstOrDefaultAsync(r => r.Id == theCategory.Id, theCancellationToken);

        if (aDocument is null)
        {
            return;
        }

        CategoryMapper.MapInto(aDocument, theCategory);
        await myMongoAppDbContext.SaveChangesAsync(theCancellationToken);
    }

    // --- Read side -------------------------------------------------------------------------
    // With no JOIN, the products are loaded once and grouped per category in memory to compute
    // the product count and stock value. Soft-deleted categories and products are excluded explicitly.

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
