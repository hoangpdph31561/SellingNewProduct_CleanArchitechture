using Microsoft.EntityFrameworkCore;
using SellingNewProduct.Domain.Categories;
using SellingNewProduct.Domain.Interfaces.Outbound;
using SellingNewProduct.Infrastructure.SqlServer.Mapping;
using SellingNewProduct.Infrastructure.SqlServer.Persistence;

namespace SellingNewProduct.Infrastructure.SqlServer.Repositories.Write;

internal sealed class SqlServerCategoryWriteRepository : ICategoryWriteRepository
{
    private readonly AppDbContext myAppDbContext;

    public SqlServerCategoryWriteRepository(AppDbContext theAppDbContext)
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

    public async Task<bool> ExistsByNameAsync(string theName, CancellationToken theCancellationToken = default)
    {
        // The soft-delete query filter already excludes Deleted rows.
        return await myAppDbContext.Categories
            .AsNoTracking()
            .AnyAsync(r => r.Name == theName, theCancellationToken);
    }

    public async Task AddAsync(Category theCategory, CancellationToken theCancellationToken = default)
    {
        myAppDbContext.Categories.Add(CategoryMapper.ToRecord(theCategory));
        await myAppDbContext.SaveChangesAsync(theCancellationToken);
    }

    public async Task UpdateAsync(Category theCategory, CancellationToken theCancellationToken = default)
    {
        var aRecord = await myAppDbContext.Categories.FirstOrDefaultAsync(r => r.Id == theCategory.Id, theCancellationToken);

        if (aRecord is null)
        {
            return;
        }

        CategoryMapper.MapInto(aRecord, theCategory);
        await myAppDbContext.SaveChangesAsync(theCancellationToken);
    }
}
