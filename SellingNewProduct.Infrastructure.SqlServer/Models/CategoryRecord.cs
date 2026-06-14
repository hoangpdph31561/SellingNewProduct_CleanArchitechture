namespace SellingNewProduct.Infrastructure.SqlServer.Models;

/// <summary>SQL Server persistence model for Category.</summary>
internal sealed class CategoryRecord : BaseRecord
{
    public string Name { get; set; } = default!;
    public string Description { get; set; } = default!;
}
