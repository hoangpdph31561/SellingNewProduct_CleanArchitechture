using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Products;
using SellingNewProduct.Domain.ValueObjects;
using SellingNewProduct.Infrastructure.SqlServer.Models;

namespace SellingNewProduct.Infrastructure.SqlServer.Mapping;

internal static class ProductMapper
{
    public static ProductRecord ToRecord(Product theProduct) => new()
    {
        Id = theProduct.Id,
        Name = theProduct.Name,
        Sku = theProduct.Sku.Value,
        Color = theProduct.Color,
        Size = (int)theProduct.Size,
        PriceAmount = theProduct.Price.Amount,
        PriceCurrency = theProduct.Price.Currency,
        StockQuantity = theProduct.StockQuantity,
        CategoryId = theProduct.CategoryId,
        Status = (int)theProduct.Status,
        CreatedAtUtc = theProduct.CreatedAtUtc,
        UpdatedAtUtc = theProduct.UpdatedAtUtc
    };

    public static void MapInto(ProductRecord theTarget, Product theSource)
    {
        theTarget.Name = theSource.Name;
        theTarget.Sku = theSource.Sku.Value;
        theTarget.Color = theSource.Color;
        theTarget.Size = (int)theSource.Size;
        theTarget.PriceAmount = theSource.Price.Amount;
        theTarget.PriceCurrency = theSource.Price.Currency;
        theTarget.StockQuantity = theSource.StockQuantity;
        theTarget.CategoryId = theSource.CategoryId;
        theTarget.Status = (int)theSource.Status;
        theTarget.UpdatedAtUtc = theSource.UpdatedAtUtc;
    }

    public static Product ToDomain(ProductRecord theRecord) => Product.Rehydrate(
        theRecord.Id,
        theRecord.Name,
        Sku.Create(theRecord.Sku),
        theRecord.Color,
        (Size)theRecord.Size,
        Money.Create(theRecord.PriceAmount, theRecord.PriceCurrency),
        theRecord.StockQuantity,
        theRecord.CategoryId,
        (EntityStatus)theRecord.Status,
        theRecord.CreatedAtUtc,
        theRecord.UpdatedAtUtc);
}
