using Microsoft.EntityFrameworkCore;
using SellingNewProduct.Domain.Products;
using SellingNewProduct.Domain.Repositories;
using SellingNewProduct.Infrastructure.SqlServer.Mapping;
using SellingNewProduct.Infrastructure.SqlServer.Persistence;

namespace SellingNewProduct.Infrastructure.SqlServer.Repositories;

internal sealed class SqlServerProductRepository : IProductRepository
{
    private readonly AppDbContext myAppDbContext;

    public SqlServerProductRepository(AppDbContext theAppDbContext)
    {
        myAppDbContext = theAppDbContext;
    }

    public async Task<Product?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default)
    {
        var aRecord = await myAppDbContext.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == theId, theCancellationToken);

        return aRecord is null ? null : ProductMapper.ToDomain(aRecord);
    }

    public async Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken theCancellationToken = default)
    {
        var aRecords = await myAppDbContext.Products.AsNoTracking().ToListAsync(theCancellationToken);
        return aRecords.Select(ProductMapper.ToDomain).ToList();
    }

    public async Task<IReadOnlyList<Product>> GetByCategoryAsync(Guid theCategoryId, CancellationToken theCancellationToken = default)
    {
        var aRecords = await myAppDbContext.Products
            .AsNoTracking()
            .Where(r => r.CategoryId == theCategoryId)
            .ToListAsync(theCancellationToken);

        return aRecords.Select(ProductMapper.ToDomain).ToList();
    }

    public async Task AddAsync(Product theProduct, CancellationToken theCancellationToken = default)
    {
        myAppDbContext.Products.Add(ProductMapper.ToRecord(theProduct));
        await myAppDbContext.SaveChangesAsync(theCancellationToken);
    }

    public async Task UpdateAsync(Product theProduct, CancellationToken theCancellationToken = default)
    {
        var aRecord = await myAppDbContext.Products.FirstOrDefaultAsync(r => r.Id == theProduct.Id, theCancellationToken);

        if (aRecord is null)
        {
            return;
        }

        ProductMapper.MapInto(aRecord, theProduct);
        await myAppDbContext.SaveChangesAsync(theCancellationToken);
    }
}
