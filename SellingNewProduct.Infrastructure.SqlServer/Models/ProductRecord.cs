namespace SellingNewProduct.Infrastructure.SqlServer.Models;

/// <summary>SQL Server persistence model for Product. Money (Price) is split into amount + currency.</summary>
internal sealed class ProductRecord
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string Sku { get; set; } = default!;
    public string Color { get; set; } = default!;
    public int Size { get; set; }
    public decimal PriceAmount { get; set; }
    public string PriceCurrency { get; set; } = default!;
    public int StockQuantity { get; set; }
    public Guid CategoryId { get; set; }
    public int Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}
