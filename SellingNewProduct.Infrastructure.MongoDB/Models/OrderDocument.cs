namespace SellingNewProduct.Infrastructure.MongoDB.Models;

/// <summary>
/// MongoDB persistence model for Order. The whole aggregate lives in ONE
/// document: the order plus its details embedded as a nested array.
/// (Contrast with SQL Server, where details are a separate table.)
/// </summary>
internal sealed class OrderDocument
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

    public List<OrderDetailDocument> Details { get; set; } = new();
}
