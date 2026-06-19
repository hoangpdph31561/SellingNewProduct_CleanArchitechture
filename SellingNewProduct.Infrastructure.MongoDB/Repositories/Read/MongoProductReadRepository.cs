using Microsoft.EntityFrameworkCore;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Products;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;
using SellingNewProduct.Domain.Interfaces.Outbound;
using SellingNewProduct.Infrastructure.MongoDB.Mapping;
using SellingNewProduct.Infrastructure.MongoDB.Models;
using SellingNewProduct.Infrastructure.MongoDB.Persistence;

namespace SellingNewProduct.Infrastructure.MongoDB.Repositories.Read;

/// <summary>
/// MongoDB read side for the catalogue. Field comparisons are pushed to the database; the name
/// "contains", sorting and paging run in memory (Mongo has no JOIN / limited text translation).
/// Soft-deleted rows are excluded explicitly.
/// </summary>
internal sealed class MongoProductReadRepository : IProductReadRepository
{
    private const int DeletedStatus = (int)EntityStatus.Deleted;

    private readonly MongoAppDbContext myMongoAppDbContext;

    public MongoProductReadRepository(MongoAppDbContext theMongoAppDbContext)
    {
        myMongoAppDbContext = theMongoAppDbContext;
    }

    public async Task<Product?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default)
    {
        var aDocument = await myMongoAppDbContext.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == theId && r.Status != DeletedStatus, theCancellationToken);

        return aDocument is null ? null : ProductMapper.ToDomain(aDocument);
    }

    public async Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken theCancellationToken = default)
    {
        var aDocuments = await myMongoAppDbContext.Products
            .AsNoTracking()
            .Where(r => r.Status != DeletedStatus)
            .ToListAsync(theCancellationToken);

        return aDocuments.Select(ProductMapper.ToDomain).ToList();
    }

    public async Task<IReadOnlyList<Product>> GetByCategoryAsync(Guid theCategoryId, CancellationToken theCancellationToken = default)
    {
        var aDocuments = await myMongoAppDbContext.Products
            .AsNoTracking()
            .Where(r => r.CategoryId == theCategoryId && r.Status != DeletedStatus)
            .ToListAsync(theCancellationToken);

        return aDocuments.Select(ProductMapper.ToDomain).ToList();
    }

    public async Task<ProductSummaryView?> GetSummaryByIdAsync(Guid theProductId, CancellationToken theCancellationToken = default)
    {
        var aProduct = await myMongoAppDbContext.Products.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == theProductId && p.Status != DeletedStatus, theCancellationToken);

        if (aProduct is null)
        {
            return null;
        }

        // No JOIN — look up the category name by id (the stitch).
        var aCategory = await myMongoAppDbContext.Categories.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == aProduct.CategoryId, theCancellationToken);

        return new ProductSummaryView(
            aProduct.Id,
            aProduct.Name,
            aProduct.Sku,
            aProduct.Color,
            aProduct.Size,
            aProduct.PriceAmount,
            aProduct.PriceCurrency,
            aProduct.StockQuantity,
            aProduct.CategoryId,
            aCategory?.Name ?? "(unknown)",
            ((EntityStatus)aProduct.Status).ToString());
    }

    public async Task<PagedResult<ProductSummaryView>> SearchAsync(
        ProductSearchQuery theQuery,
        CancellationToken theCancellationToken = default)
    {
        var aPage = new PageRequest(theQuery.Page, theQuery.PageSize);

        var aQuery = myMongoAppDbContext.Products.AsNoTracking()
            .Where(p => p.Status != DeletedStatus);

        if (theQuery.CategoryId is not null)
        {
            aQuery = aQuery.Where(p => p.CategoryId == theQuery.CategoryId);
        }

        if (theQuery.PriceFrom is not null)
        {
            aQuery = aQuery.Where(p => p.PriceAmount >= theQuery.PriceFrom);
        }

        if (theQuery.PriceTo is not null)
        {
            aQuery = aQuery.Where(p => p.PriceAmount <= theQuery.PriceTo);
        }

        if (theQuery.MinStock is not null)
        {
            aQuery = aQuery.Where(p => p.StockQuantity >= theQuery.MinStock);
        }

        if (theQuery.MaxStock is not null)
        {
            aQuery = aQuery.Where(p => p.StockQuantity <= theQuery.MaxStock);
        }

        if (theQuery.Status is not null)
        {
            var aStatusValue = (int)theQuery.Status.Value;
            aQuery = aQuery.Where(p => p.Status == aStatusValue);
        }

        var aCandidates = await aQuery.ToListAsync(theCancellationToken);

        IEnumerable<ProductDocument> aFiltered = aCandidates;

        if (!string.IsNullOrWhiteSpace(theQuery.Name))
        {
            var aName = theQuery.Name.Trim();
            aFiltered = aFiltered.Where(p => p.Name.Contains(aName, StringComparison.OrdinalIgnoreCase));
        }

        aFiltered = (theQuery.SortBy?.Trim().ToLowerInvariant()) switch
        {
            "price" => theQuery.SortDescending ? aFiltered.OrderByDescending(p => p.PriceAmount) : aFiltered.OrderBy(p => p.PriceAmount),
            "stock" => theQuery.SortDescending ? aFiltered.OrderByDescending(p => p.StockQuantity) : aFiltered.OrderBy(p => p.StockQuantity),
            _ => theQuery.SortDescending ? aFiltered.OrderByDescending(p => p.Name) : aFiltered.OrderBy(p => p.Name)
        };

        var aMatched = aFiltered.ToList();
        var aTotalCount = aMatched.Count;

        var aPageDocs = aMatched
            .Skip(aPage.Skip)
            .Take(aPage.PageSize)
            .ToList();

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
