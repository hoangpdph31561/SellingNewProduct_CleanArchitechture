namespace SellingNewProduct.Infrastructure.SqlServer.Models;

/// <summary>SQL Server persistence model for Payment.</summary>
internal sealed class PaymentRecord : BaseRecord
{
    public Guid OrderId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = default!;
    public int Method { get; set; }
    public int PaymentStatus { get; set; }
    public DateTime? PaidAtUtc { get; set; }
}
