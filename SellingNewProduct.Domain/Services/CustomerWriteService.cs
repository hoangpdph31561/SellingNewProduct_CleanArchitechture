using SellingNewProduct.Domain.Customers;
using SellingNewProduct.Domain.Interfaces.Inbound;
using SellingNewProduct.Domain.Interfaces.Outbound;
using SellingNewProduct.Domain.ValueObjects;

namespace SellingNewProduct.Domain.Services;

/// <summary>Implements the customer inbound port. Composes the address and email value objects.</summary>
public sealed class CustomerWriteService : ICustomerWriteService
{
    private readonly ICustomerWriteRepository myCustomerRepository;

    public CustomerWriteService(ICustomerWriteRepository theCustomerRepository)
    {
        myCustomerRepository = theCustomerRepository;
    }

    public async Task<Customer> CreateAsync(NewCustomer theRequest, CancellationToken theCancellationToken = default)
    {
        var aAddress = Address.Create(
            theRequest.Street, theRequest.Ward, theRequest.District, theRequest.City, theRequest.Country);

        var aCustomer = Customer.Create(
            theRequest.FullName,
            Email.Create(theRequest.Email),
            theRequest.PhoneNumber,
            aAddress,
            theRequest.UserId);

        await myCustomerRepository.AddAsync(aCustomer, theCancellationToken);
        return aCustomer;
    }

    public Task DeleteAsync(Guid theId, CancellationToken theCancellationToken = default)
        => myCustomerRepository.DeleteAsync(theId, theCancellationToken);
}
