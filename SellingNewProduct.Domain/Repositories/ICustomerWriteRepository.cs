using SellingNewProduct.Domain.Customers;

namespace SellingNewProduct.Domain.Repositories;

public interface ICustomerWriteRepository
{
    /// <summary>Loads a customer aggregate (used by the order flow "customer must exist").</summary>
    Task<Customer?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default);

    Task AddAsync(Customer theCustomer, CancellationToken theCancellationToken = default);

    Task UpdateAsync(Customer theCustomer, CancellationToken theCancellationToken = default);

    Task DeleteAsync(Guid theId, CancellationToken theCancellationToken = default);
}
