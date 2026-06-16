namespace SellingNewProduct.Domain.Commands;

/// <summary>
/// Input for <c>IProductService.CreateAsync</c>: the data needed to create a product,
/// gathered into one object instead of a long parameter list. The API maps its request DTO
/// to this command (see <c>ApiMappings.ToCommand</c>); the service turns it into the
/// <c>Product</c> aggregate.
/// </summary>
public sealed record CreateProductCommand(
    string Name,
    string Sku,
    string Color,
    int Size,
    decimal Price,
    string Currency,
    int StockQuantity,
    Guid CategoryId);
