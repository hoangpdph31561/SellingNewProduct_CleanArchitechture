namespace SellingNewProduct.Infrastructure.SqlServer.Models;

/// <summary>SQL Server persistence model for Customer. Address is flattened into columns.</summary>
internal sealed class CustomerRecord
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string PhoneNumber { get; set; } = default!;
    public string Street { get; set; } = default!;
    public string Ward { get; set; } = default!;
    public string District { get; set; } = default!;
    public string City { get; set; } = default!;
    public string Country { get; set; } = default!;
    public Guid? UserId { get; set; }
    public int Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}
