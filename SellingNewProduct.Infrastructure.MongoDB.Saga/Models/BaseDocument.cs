namespace SellingNewProduct.Infrastructure.MongoDB.Saga.Models;

/// <summary>
/// Common fields shared by every MongoDB persistence model in the saga provider: identity,
/// soft-delete status and audit timestamps — the same fields carried by the domain's
/// <c>BaseEntity</c>.
/// </summary>
internal abstract class BaseDocument
{
    public Guid Id { get; set; }
    public int Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}
