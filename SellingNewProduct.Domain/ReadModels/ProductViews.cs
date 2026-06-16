namespace SellingNewProduct.Domain.ReadModels;

/// <summary>
/// Flat read-model for a product list/search screen, enriched with the category
/// NAME via a JOIN — so the grid can show the category without a second lookup per
/// row. This is read side only, not the <c>Product</c> aggregate: it exists for display.
/// </summary>
public sealed record ProductSummaryView(
    Guid ProductId,
    string Name,
    string Sku,
    string Color,
    int Size,
    decimal Price,
    string Currency,
    int StockQuantity,
    Guid CategoryId,
    string CategoryName,
    string Status);
