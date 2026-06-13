namespace SellingNewProduct.API.Contracts;

public sealed record CreateProductRequest(
    string Name,
    string Sku,
    string Color,
    int Size,
    decimal Price,
    string Currency,
    int StockQuantity,
    Guid CategoryId);

public sealed record ProductResponse(
    Guid Id,
    string Name,
    string Sku,
    string Color,
    int Size,
    decimal Price,
    string Currency,
    int StockQuantity,
    Guid CategoryId,
    string Status);
