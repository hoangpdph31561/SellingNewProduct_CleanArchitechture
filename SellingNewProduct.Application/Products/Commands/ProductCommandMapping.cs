using SellingNewProduct.Domain.Products;

namespace SellingNewProduct.Application.Products;

/// <summary>Maps the application command to the Domain-owned <see cref="NewProduct"/> input.</summary>
internal static class ProductCommandMapping
{
    public static NewProduct ToNewProduct(this CreateProductCommand theCommand) =>
        new(
            theCommand.Name,
            theCommand.Sku,
            theCommand.Color,
            theCommand.Size,
            theCommand.Price,
            theCommand.Currency,
            theCommand.StockQuantity,
            theCommand.CategoryId);
}
