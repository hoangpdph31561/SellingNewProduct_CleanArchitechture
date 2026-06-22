using SellingNewProduct.Domain.Customers;

namespace SellingNewProduct.Domain.Interfaces.Outbound;

public interface ICustomerWriteRepository : IWriteRepository<Customer>
{
    /// <summary>Loads a customer aggregate (used by the order flow "customer must exist").</summary>
    Task<Customer?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default);

    Task DeleteAsync(Guid theId, CancellationToken theCancellationToken = default);
}
