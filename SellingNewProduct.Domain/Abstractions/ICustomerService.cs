using SellingNewProduct.Domain.Commands;
using SellingNewProduct.Domain.Customers;

namespace SellingNewProduct.Domain.Abstractions;

/// <summary>
/// Customer write-side behavior. The API depends on this, not on the repository.
/// (Order history is read side — see <c>IOrderQueries</c>.)
/// </summary>
public interface ICustomerService
{
    Task<IReadOnlyList<Customer>> GetAllAsync(CancellationToken theCancellationToken = default);

    Task<Customer?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default);

    Task<Customer> CreateAsync(CreateCustomerCommand theCommand, CancellationToken theCancellationToken = default);

    Task DeleteAsync(Guid theId, CancellationToken theCancellationToken = default);
}
