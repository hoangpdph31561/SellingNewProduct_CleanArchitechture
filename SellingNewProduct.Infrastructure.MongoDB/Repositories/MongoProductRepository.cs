using Microsoft.EntityFrameworkCore;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Products;
using SellingNewProduct.Domain.Repositories;
using SellingNewProduct.Infrastructure.MongoDB.Mapping;
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

    public async Task<IReadOnlyList<Product>> GetByCategoryAsync(Guid theCategoryId, CancellationToken theCancellationToken = default)
    {
        var aDocuments = await myMongoAppDbContext.Products
            .AsNoTracking()
            .Where(r => r.CategoryId == theCategoryId && r.Status != DeletedStatus)
            .ToListAsync(theCancellationToken);

        return aDocuments.Select(ProductMapper.ToDomain).ToList();
    }

    public async Task AddAsync(Product theProduct, CancellationToken theCancellationToken = default)
    {
        myMongoAppDbContext.Products.Add(ProductMapper.ToDocument(theProduct));
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
}
