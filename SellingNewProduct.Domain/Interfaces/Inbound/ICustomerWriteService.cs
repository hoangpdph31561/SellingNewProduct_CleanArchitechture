using SellingNewProduct.Domain.Customers;

namespace SellingNewProduct.Domain.Interfaces.Inbound;

/// <summary>Inbound (driving) port for customer writes.</summary>
public interface ICustomerWriteService : IWriteService<Customer>
{
    /// <summary>Creates a customer, composing its address and email value objects.</summary>
    Task<Customer> CreateAsync(NewCustomer theRequest, CancellationToken theCancellationToken = default);

    /// <summary>Deletes a customer by id.</summary>
    Task DeleteAsync(Guid theId, CancellationToken theCancellationToken = default);
}
