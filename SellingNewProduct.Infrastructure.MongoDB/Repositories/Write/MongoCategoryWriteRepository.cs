using Microsoft.EntityFrameworkCore;
using SellingNewProduct.Domain.Categories;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Interfaces.Outbound;
using SellingNewProduct.Infrastructure.MongoDB.Mapping;
using SellingNewProduct.Infrastructure.MongoDB.Persistence;

namespace SellingNewProduct.Infrastructure.MongoDB.Repositories.Write;

internal sealed class MongoCategoryWriteRepository : ICategoryWriteRepository
{
    private const int DeletedStatus = (int)EntityStatus.Deleted;

    private readonly MongoAppDbContext myMongoAppDbContext;

    public MongoCategoryWriteRepository(MongoAppDbContext theMongoAppDbContext)
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

    public async Task<bool> ExistsByNameAsync(string theName, CancellationToken theCancellationToken = default)
    {
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
}
