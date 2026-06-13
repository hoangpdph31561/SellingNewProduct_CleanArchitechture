namespace SellingNewProduct.Infrastructure.SqlServer.Models;

/// <summary>
/// SQL Server persistence model for Order. In SQL the aggregate is normalised
/// into two tables: Orders + OrderDetails (one-to-many, FK + cascade delete).
/// </summary>
internal sealed class OrderRecord
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public Guid EmployeeId { get; set; }
    public int OrderStatus { get; set; }
    public DateTime OrderDate { get; set; }

    public string ShippingStreet { get; set; } = default!;
    public string ShippingWard { get; set; } = default!;
    public string ShippingDistrict { get; set; } = default!;
    public string ShippingCity { get; set; } = default!;
    public string ShippingCountry { get; set; } = default!;

    public decimal TotalAmount { get; set; }
    public string TotalCurrency { get; set; } = default!;

    public int Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    public List<OrderDetailRecord> Details { get; set; } = new();
}
