using SellingNewProduct.Domain.Payments;

namespace SellingNewProduct.Domain.Repositories;

public interface IPaymentRepository
{
    Task<Payment?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default);

    Task<IReadOnlyList<Payment>> GetByOrderAsync(Guid theOrderId, CancellationToken theCancellationToken = default);

    Task AddAsync(Payment thePayment, CancellationToken theCancellationToken = default);

    Task UpdateAsync(Payment thePayment, CancellationToken theCancellationToken = default);
}
