namespace SellingNewProduct.Infrastructure.MongoDB.Saga.Models;

/// <summary>MongoDB persistence model for Category.</summary>
internal sealed class CategoryDocument : BaseDocument
{
    public string Name { get; set; } = default!;
    public string Description { get; set; } = default!;
}
