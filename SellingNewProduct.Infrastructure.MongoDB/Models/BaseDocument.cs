namespace SellingNewProduct.Infrastructure.MongoDB.Models;

/// <summary>
/// Common fields shared by every MongoDB persistence model: identity,
/// soft-delete status and audit timestamps — the same fields carried by the
/// domain's <c>BaseEntity</c>. Embedded documents (e.g. order lines) reuse it too.
/// </summary>
internal abstract class BaseDocument
{
    public Guid Id { get; set; }
    public int Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}
