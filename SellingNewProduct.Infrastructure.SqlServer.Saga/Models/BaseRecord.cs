namespace SellingNewProduct.Infrastructure.SqlServer.Saga.Models;

/// <summary>
/// Common columns shared by every SQL Server persistence model in this (saga) provider:
/// identity, soft-delete status and audit timestamps — the same fields carried by the domain's
/// <c>BaseEntity</c>. Not an entity itself.
/// </summary>
internal abstract class BaseRecord
{
    public Guid Id { get; set; }
    public int Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}
