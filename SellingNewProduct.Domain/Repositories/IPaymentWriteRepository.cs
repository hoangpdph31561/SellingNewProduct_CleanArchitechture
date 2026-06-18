using SellingNewProduct.Domain.Payments;

namespace SellingNewProduct.Domain.Repositories;

public interface IPaymentWriteRepository
{
    /// <summary>Loads a payment aggregate to mutate it (e.g. mark completed).</summary>
    Task<Payment?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default);

    /// <summary>All payments of an order — the create rule sums completed payments to prevent overpayment.</summary>
    Task<IReadOnlyList<Payment>> GetByOrderAsync(Guid theOrderId, CancellationToken theCancellationToken = default);

    Task AddAsync(Payment thePayment, CancellationToken theCancellationToken = default);

    Task UpdateAsync(Payment thePayment, CancellationToken theCancellationToken = default);
}
