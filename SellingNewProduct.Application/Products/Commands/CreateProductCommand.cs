using MediatR;
using SellingNewProduct.Domain.Products;

namespace SellingNewProduct.Application.Products;

/// <summary>Write-side command: create a single product. Returns the created aggregate.</summary>
public sealed record CreateProductCommand(
    string Name,
    string Sku,
    string Color,
    int Size,
    decimal Price,
    string Currency,
    int StockQuantity,
    Guid CategoryId) : IRequest<Product>;
