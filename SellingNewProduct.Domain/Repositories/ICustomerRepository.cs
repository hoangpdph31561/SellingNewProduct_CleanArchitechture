using SellingNewProduct.Domain.Customers;

namespace SellingNewProduct.Domain.Repositories;

public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default);

    Task<IReadOnlyList<Customer>> GetAllAsync(CancellationToken theCancellationToken = default);

    Task AddAsync(Customer theCustomer, CancellationToken theCancellationToken = default);

    Task UpdateAsync(Customer theCustomer, CancellationToken theCancellationToken = default);
}
