using Microsoft.EntityFrameworkCore;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Products;
using SellingNewProduct.Domain.Repositories;
using SellingNewProduct.Infrastructure.MongoDB.Mapping;
using SellingNewProduct.Infrastructure.MongoDB.Persistence;

namespace SellingNewProduct.Infrastructure.MongoDB.Repositories.Write;

internal sealed class MongoProductWriteRepository : IProductWriteRepository
{
    private const int DeletedStatus = (int)EntityStatus.Deleted;

    private readonly MongoAppDbContext myMongoAppDbContext;

    public MongoProductWriteRepository(MongoAppDbContext theMongoAppDbContext)
    {
        myMongoAppDbContext = theMongoAppDbContext;
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
}
