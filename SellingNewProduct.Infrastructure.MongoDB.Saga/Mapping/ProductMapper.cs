using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Products;
using SellingNewProduct.Domain.ValueObjects;
using SellingNewProduct.Infrastructure.MongoDB.Saga.Models;

namespace SellingNewProduct.Infrastructure.MongoDB.Saga.Mapping;

internal static class ProductMapper
{
    public static ProductDocument ToDocument(Product theProduct) => new()
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

    public static void MapInto(ProductDocument theTarget, Product theSource)
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

    public static Product ToDomain(ProductDocument theDocument) => Product.Rehydrate(
        theDocument.Id,
        theDocument.Name,
        Sku.Create(theDocument.Sku),
        theDocument.Color,
        (Size)theDocument.Size,
        Money.Create(theDocument.PriceAmount, theDocument.PriceCurrency),
        theDocument.StockQuantity,
        theDocument.CategoryId,
        (EntityStatus)theDocument.Status,
        theDocument.CreatedAtUtc,
        theDocument.UpdatedAtUtc);
}
