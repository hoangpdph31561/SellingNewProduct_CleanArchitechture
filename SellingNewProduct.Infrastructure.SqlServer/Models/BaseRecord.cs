namespace SellingNewProduct.Infrastructure.SqlServer.Models;

/// <summary>
/// Common columns shared by every SQL Server persistence model: identity,
/// soft-delete status and audit timestamps — the same fields carried by the
/// domain's <c>BaseEntity</c>. Not an entity itself; each derived record is
/// mapped to its own table, so these columns are emitted on every table.
/// </summary>
internal abstract class BaseRecord
{
    public Guid Id { get; set; }
    public int Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}
