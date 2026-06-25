namespace SellingNewProduct.Infrastructure.SqlServer.Saga.Models;

/// <summary>
/// SQL Server persistence model for Order in the saga provider. Normalised into two tables:
/// Orders + OrderDetails. Customer/Employee/Product are NOT SQL tables here (they live in
/// MongoDB), so the cross-aggregate ids are plain columns with no foreign key.
/// </summary>
internal sealed class OrderRecord : BaseRecord
{
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

    public List<OrderDetailRecord> Details { get; set; } = new();
}
