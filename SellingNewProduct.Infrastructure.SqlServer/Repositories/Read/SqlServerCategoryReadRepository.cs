using Microsoft.EntityFrameworkCore;
using SellingNewProduct.Domain.Categories;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;
using SellingNewProduct.Domain.Repositories;
using SellingNewProduct.Infrastructure.SqlServer.Mapping;
using SellingNewProduct.Infrastructure.SqlServer.Persistence;

namespace SellingNewProduct.Infrastructure.SqlServer.Repositories.Read;

/// <summary>
/// SQL Server read side for categories. The product count and stock value are correlated
/// subqueries (COUNT / SUM over Products) pushed into the same SQL statement. Soft-deleted
/// rows are excluded by the Global Query Filters.
/// </summary>
internal sealed class SqlServerCategoryReadRepository : ICategoryReadRepository
{
    private readonly AppDbContext myAppDbContext;

    public SqlServerCategoryReadRepository(AppDbContext theAppDbContext)
    {
        myAppDbContext = theAppDbContext;
    }

    public async Task<Category?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default)
    {
        var aRecord = await myAppDbContext.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == theId, theCancellationToken);

        return aRecord is null ? null : CategoryMapper.ToDomain(aRecord);
    }

    public async Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken theCancellationToken = default)
    {
        var aRecords = await myAppDbContext.Categories.AsNoTracking().ToListAsync(theCancellationToken);
        return aRecords.Select(CategoryMapper.ToDomain).ToList();
    }

    public async Task<IReadOnlyList<CategorySummaryView>> GetCategorySummariesAsync(CancellationToken theCancellationToken = default)
    {
        var aRows = await BuildSummaryQuery(myAppDbContext.Categories.AsNoTracking())
            .OrderBy(r => r.Name)
            .ToListAsync(theCancellationToken);

        return aRows.Select(ToView).ToList();
    }

    public async Task<PagedResult<CategorySummaryView>> SearchAsync(
        CategorySearchQuery theQuery,
        CancellationToken theCancellationToken = default)
    {
        var aPage = new PageRequest(theQuery.Page, theQuery.PageSize);

        var aSource = myAppDbContext.Categories.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(theQuery.Name))
        {
            var aName = theQuery.Name.Trim();
            aSource = aSource.Where(c => c.Name.Contains(aName));
        }

        var aTotalCount = await aSource.CountAsync(theCancellationToken);

        var aQuery = BuildSummaryQuery(aSource);
        aQuery = theQuery.SortDescending ? aQuery.OrderByDescending(r => r.Name) : aQuery.OrderBy(r => r.Name);

        var aRows = await aQuery
            .Skip(aPage.Skip)
            .Take(aPage.PageSize)
            .ToListAsync(theCancellationToken);

        var aItems = aRows.Select(ToView).ToList();

        return new PagedResult<CategorySummaryView>(aItems, aPage.Page, aPage.PageSize, aTotalCount);
    }

    // Shared projection: category + correlated product count and stock value. Status is
    // kept as an int here and mapped to its enum name in memory (EF cannot translate enum.ToString()).
    private IQueryable<CategorySummaryRow> BuildSummaryQuery(IQueryable<Models.CategoryRecord> theSource) =>
        theSource.Select(c => new CategorySummaryRow(
            c.Id,
            c.Name,
            c.Description,
            c.Status,
            myAppDbContext.Products.Count(p => p.CategoryId == c.Id),
            myAppDbContext.Products.Where(p => p.CategoryId == c.Id).Sum(p => (decimal?)(p.PriceAmount * p.StockQuantity)) ?? 0m));

    private static CategorySummaryView ToView(CategorySummaryRow theRow) =>
        new(theRow.Id, theRow.Name, theRow.Description, theRow.ProductCount, theRow.TotalStockValue, ((EntityStatus)theRow.Status).ToString());

    private sealed record CategorySummaryRow(
        Guid Id,
        string Name,
        string Description,
        int Status,
        int ProductCount,
        decimal TotalStockValue);
}
