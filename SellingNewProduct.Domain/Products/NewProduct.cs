namespace SellingNewProduct.Domain.Products;

/// <summary>
/// A request to create one product. A Domain-owned input for the product inbound port, so its
/// signature does not depend on the application layer's command type.
/// </summary>
public sealed record NewProduct(
    string Name,
    string Sku,
    string Color,
    int Size,
    decimal Price,
    string Currency,
    int StockQuantity,
    Guid CategoryId);
