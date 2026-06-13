using SellingNewProduct.Domain.Categories;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Infrastructure.SqlServer.Models;

namespace SellingNewProduct.Infrastructure.SqlServer.Mapping;

internal static class CategoryMapper
{
    public static CategoryRecord ToRecord(Category theCategory) => new()
    {
        Id = theCategory.Id,
        Name = theCategory.Name,
        Description = theCategory.Description,
        Status = (int)theCategory.Status,
        CreatedAtUtc = theCategory.CreatedAtUtc,
        UpdatedAtUtc = theCategory.UpdatedAtUtc
    };

    public static void MapInto(CategoryRecord theTarget, Category theSource)
    {
        theTarget.Name = theSource.Name;
        theTarget.Description = theSource.Description;
        theTarget.Status = (int)theSource.Status;
        theTarget.UpdatedAtUtc = theSource.UpdatedAtUtc;
    }

    public static Category ToDomain(CategoryRecord theRecord) => Category.Rehydrate(
        theRecord.Id,
        theRecord.Name,
        theRecord.Description,
        (EntityStatus)theRecord.Status,
        theRecord.CreatedAtUtc,
        theRecord.UpdatedAtUtc);
}
