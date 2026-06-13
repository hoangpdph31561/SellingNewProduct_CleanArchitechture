namespace SellingNewProduct.Infrastructure.MongoDB.Models;

/// <summary>
/// Embedded order line inside an <see cref="OrderDocument"/>. Unlike SQL, this
/// is NOT a separate collection — it lives nested in the order document.
/// </summary>
internal sealed class OrderDetailDocument
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = default!;
    public decimal UnitPriceAmount { get; set; }
    public string UnitPriceCurrency { get; set; } = default!;
    public int Quantity { get; set; }
    public int Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}
