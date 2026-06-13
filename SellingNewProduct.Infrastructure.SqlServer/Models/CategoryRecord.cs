namespace SellingNewProduct.Infrastructure.SqlServer.Models;

/// <summary>SQL Server persistence model for Category.</summary>
internal sealed class CategoryRecord
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string Description { get; set; } = default!;
    public int Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}
