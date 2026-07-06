using SellingNewProduct.Domain.Common;

namespace SellingNewProduct.Domain.Products;

/// <summary>
/// Raised when a product's stock moves (reserved on order confirm, returned on cancel).
/// <see cref="ChangeQuantity"/> is negative when stock went down, positive when it went up.
/// </summary>
public sealed class ProductStockChangedEvent : IDomainEvent
{
    public Guid ProductId { get; }

    public string ProductName { get; }

    public int NewStock { get; }

    public int ChangeQuantity { get; }

    public DateTime OccurredOnUtc { get; }

    public ProductStockChangedEvent(Guid theProductId, string theProductName, int theNewStock, int theChangeQuantity)
    {
        ProductId = theProductId;
        ProductName = theProductName;
        NewStock = theNewStock;
        ChangeQuantity = theChangeQuantity;
        OccurredOnUtc = DateTime.UtcNow;
    }
}
