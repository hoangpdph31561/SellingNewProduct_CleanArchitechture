namespace SellingNewProduct.Infrastructure.MongoDB.Models;

/// <summary>MongoDB persistence model for Category.</summary>
internal sealed class CategoryDocument
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string Description { get; set; } = default!;
    public int Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}
