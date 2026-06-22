using SellingNewProduct.Domain.Payments;
using SellingNewProduct.Domain.ValueObjects;

namespace SellingNewProduct.Domain.Interfaces.Inbound;

/// <summary>
/// Inbound (driving) port for payments. Owns the rules that relate a payment to the order it
/// settles (payable status, matching currency, no overpayment).
/// </summary>
public interface IPaymentWriteService : IWriteService<Payment>
{
    /// <summary>Records a payment against an order after enforcing the settlement rules.</summary>
    Task<Payment> CreateAsync(
        Guid theOrderId,
        Money theAmount,
        PaymentMethod theMethod,
        CancellationToken theCancellationToken = default);

    /// <summary>Marks an existing payment as completed.</summary>
    Task<Payment> CompleteAsync(Guid thePaymentId, CancellationToken theCancellationToken = default);
}
