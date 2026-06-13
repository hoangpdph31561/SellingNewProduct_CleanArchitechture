namespace SellingNewProduct.Infrastructure.MongoDB.Models;

/// <summary>MongoDB persistence model for Employee.</summary>
internal sealed class EmployeeDocument
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = default!;
    public string Position { get; set; } = default!;
    public DateTime HireDate { get; set; }
    public Guid UserId { get; set; }
    public int Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}
