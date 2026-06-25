using SellingNewProduct.Domain.Categories;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Infrastructure.MongoDB.Saga.Models;

namespace SellingNewProduct.Infrastructure.MongoDB.Saga.Mapping;

internal static class CategoryMapper
{
    public static CategoryDocument ToDocument(Category theCategory) => new()
    {
        Id = theCategory.Id,
        Name = theCategory.Name,
        Description = theCategory.Description,
        Status = (int)theCategory.Status,
        CreatedAtUtc = theCategory.CreatedAtUtc,
        UpdatedAtUtc = theCategory.UpdatedAtUtc
    };

    public static void MapInto(CategoryDocument theTarget, Category theSource)
    {
        theTarget.Name = theSource.Name;
        theTarget.Description = theSource.Description;
        theTarget.Status = (int)theSource.Status;
        theTarget.UpdatedAtUtc = theSource.UpdatedAtUtc;
    }

    public static Category ToDomain(CategoryDocument theDocument) => Category.Rehydrate(
        theDocument.Id,
        theDocument.Name,
        theDocument.Description,
        (EntityStatus)theDocument.Status,
        theDocument.CreatedAtUtc,
        theDocument.UpdatedAtUtc);
}
