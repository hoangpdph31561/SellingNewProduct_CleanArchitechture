using SellingNewProduct.Domain.Commands;
using SellingNewProduct.Domain.Payments;

namespace SellingNewProduct.Domain.Abstractions;

/// <summary>Payment write-side behavior. The API depends on this, not on the repositories.</summary>
public interface IPaymentService
{
    Task<Payment?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default);

    Task<Payment> CreateAsync(CreatePaymentCommand theCommand, CancellationToken theCancellationToken = default);

    Task<Payment> CompleteAsync(Guid thePaymentId, CancellationToken theCancellationToken = default);
}
