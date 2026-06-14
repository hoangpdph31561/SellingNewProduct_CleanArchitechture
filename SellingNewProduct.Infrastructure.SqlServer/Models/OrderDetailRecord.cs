namespace SellingNewProduct.Infrastructure.SqlServer.Models;

/// <summary>SQL Server persistence model for an order line (child table of Orders).</summary>
internal sealed class OrderDetailRecord : BaseRecord
{
    public Guid OrderId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = default!;
    public decimal UnitPriceAmount { get; set; }
    public string UnitPriceCurrency { get; set; } = default!;
    public int Quantity { get; set; }
}
