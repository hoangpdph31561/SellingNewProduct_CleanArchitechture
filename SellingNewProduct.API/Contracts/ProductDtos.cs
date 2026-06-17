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

/// <summary>Bulk create: several products in one request (maps to <c>IProductService.CreateManyAsync</c>).</summary>
public sealed record BulkCreateProductsRequest(IReadOnlyList<CreateProductRequest> Items);

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
