using Microsoft.EntityFrameworkCore;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Products;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;
using SellingNewProduct.Domain.Repositories;
using SellingNewProduct.Infrastructure.MongoDB.Mapping;
using SellingNewProduct.Infrastructure.MongoDB.Models;
using SellingNewProduct.Infrastructure.MongoDB.Persistence;

namespace SellingNewProduct.Infrastructure.MongoDB.Repositories;

internal sealed class MongoProductRepository : IProductRepository
{
    private const int DeletedStatus = (int)EntityStatus.Deleted;

    private readonly MongoAppDbContext myMongoAppDbContext;

    public MongoProductRepository(MongoAppDbContext theMongoAppDbContext)
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

    public async Task<IReadOnlyList<Product>> GetByIdsAsync(IReadOnlyCollection<Guid> theIds, CancellationToken theCancellationToken = default)
    {
        if (theIds.Count == 0)
        {
            return [];
        }

        var aDocuments = await myMongoAppDbContext.Products
            .AsNoTracking()
            .Where(r => theIds.Contains(r.Id) && r.Status != DeletedStatus)
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

    public async Task<bool> ExistsBySkuAsync(string theSku, CancellationToken theCancellationToken = default)
    {
        // Mongo has no global query filter, so exclude soft-deleted rows here.
        return await myMongoAppDbContext.Products
            .AsNoTracking()
            .AnyAsync(r => r.Sku == theSku && r.Status != DeletedStatus, theCancellationToken);
    }

    public async Task AddAsync(Product theProduct, CancellationToken theCancellationToken = default)
    {
        myMongoAppDbContext.Products.Add(ProductMapper.ToDocument(theProduct));
        await myMongoAppDbContext.SaveChangesAsync(theCancellationToken);
    }

    public async Task AddRangeAsync(IEnumerable<Product> theProducts, CancellationToken theCancellationToken = default)
    {
        myMongoAppDbContext.Products.AddRange(theProducts.Select(ProductMapper.ToDocument));
        await myMongoAppDbContext.SaveChangesAsync(theCancellationToken);
    }

    public async Task UpdateAsync(Product theProduct, CancellationToken theCancellationToken = default)
    {
        var aDocument = await myMongoAppDbContext.Products.FirstOrDefaultAsync(r => r.Id == theProduct.Id, theCancellationToken);

        if (aDocument is null)
        {
            return;
        }

        ProductMapper.MapInto(aDocument, theProduct);
        await myMongoAppDbContext.SaveChangesAsync(theCancellationToken);
    }

    public async Task UpdateRangeAsync(IEnumerable<Product> theProducts, CancellationToken theCancellationToken = default)
    {
        var aProducts = theProducts.ToList();
        var aIds = aProducts.Select(p => p.Id).ToList();

        var aDocuments = (await myMongoAppDbContext.Products
            .Where(r => aIds.Contains(r.Id))
            .ToListAsync(theCancellationToken))
            .ToDictionary(r => r.Id);

        foreach (var aProduct in aProducts)
        {
            if (aDocuments.TryGetValue(aProduct.Id, out var aDocument))
            {
                ProductMapper.MapInto(aDocument, aProduct);
            }
        }

        await myMongoAppDbContext.SaveChangesAsync(theCancellationToken);
    }

    // --- Read side -------------------------------------------------------------------------
    // Field comparisons are pushed to the database; the name "contains", sorting and paging
    // run in memory (Mongo has no JOIN / limited text translation). Soft-deleted rows excluded explicitly.

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

        // Push the field comparisons to the database to shrink the candidate set.
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

        // Name "contains" + sorting in memory (Mongo has no JOIN/limited text translation).
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
