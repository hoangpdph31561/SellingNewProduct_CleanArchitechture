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

    /// <summary>
    /// Completes the pending payment of an order — the entry point a payment-gateway callback (e.g. VNPay
    /// return/IPN) uses, since it only knows the order id. Idempotent: a duplicate callback finds the
    /// payment already completed and returns it unchanged. Returns null when the order has no payment to
    /// complete (none was recorded), so the caller can react without treating it as an error.
    /// </summary>
    Task<Payment?> CompleteByOrderAsync(Guid theOrderId, CancellationToken theCancellationToken = default);
}
