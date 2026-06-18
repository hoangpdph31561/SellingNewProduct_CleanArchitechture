using Microsoft.EntityFrameworkCore;
using SellingNewProduct.Domain.Products;
using SellingNewProduct.Domain.Repositories;
using SellingNewProduct.Infrastructure.SqlServer.Mapping;
using SellingNewProduct.Infrastructure.SqlServer.Persistence;

namespace SellingNewProduct.Infrastructure.SqlServer.Repositories.Write;

/// <summary>SQL Server write side for the product aggregate (load-to-mutate + persistence).</summary>
internal sealed class SqlServerProductWriteRepository : IProductWriteRepository
{
    private readonly AppDbContext myAppDbContext;

    public SqlServerProductWriteRepository(AppDbContext theAppDbContext)
    {
        myAppDbContext = theAppDbContext;
    }

    public async Task<IReadOnlyList<Product>> GetByIdsAsync(IReadOnlyCollection<Guid> theIds, CancellationToken theCancellationToken = default)
    {
        if (theIds.Count == 0)
        {
            return [];
        }

        var aRecords = await myAppDbContext.Products
            .AsNoTracking()
            .Where(r => theIds.Contains(r.Id))
            .ToListAsync(theCancellationToken);

        return aRecords.Select(ProductMapper.ToDomain).ToList();
    }

    public async Task<bool> ExistsBySkuAsync(string theSku, CancellationToken theCancellationToken = default)
    {
        // The soft-delete query filter already excludes Deleted rows.
        return await myAppDbContext.Products
            .AsNoTracking()
            .AnyAsync(r => r.Sku == theSku, theCancellationToken);
    }

    public async Task AddAsync(Product theProduct, CancellationToken theCancellationToken = default)
    {
        myAppDbContext.Products.Add(ProductMapper.ToRecord(theProduct));
        await myAppDbContext.SaveChangesAsync(theCancellationToken);
    }

    public async Task AddRangeAsync(IEnumerable<Product> theProducts, CancellationToken theCancellationToken = default)
    {
        myAppDbContext.Products.AddRange(theProducts.Select(ProductMapper.ToRecord));
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

    public async Task UpdateRangeAsync(IEnumerable<Product> theProducts, CancellationToken theCancellationToken = default)
    {
        var aProducts = theProducts.ToList();
        var aIds = aProducts.Select(p => p.Id).ToList();

        // Load the tracked records once, then map each domain product into its record.
        var aRecords = await myAppDbContext.Products
            .Where(r => aIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, theCancellationToken);

        foreach (var aProduct in aProducts)
        {
            if (aRecords.TryGetValue(aProduct.Id, out var aRecord))
            {
                ProductMapper.MapInto(aRecord, aProduct);
            }
        }

        await myAppDbContext.SaveChangesAsync(theCancellationToken);
    }
}
