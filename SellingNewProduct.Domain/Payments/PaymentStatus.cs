namespace SellingNewProduct.Domain.Payments;

/// <summary>Business status of a payment (distinct from the soft-delete EntityStatus).</summary>
public enum PaymentStatus
{
    Pending = 1,
    Completed = 2,
    Refunded = 3
}
