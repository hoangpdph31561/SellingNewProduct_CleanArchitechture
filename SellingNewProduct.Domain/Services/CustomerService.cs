using SellingNewProduct.Domain.Abstractions;
using SellingNewProduct.Domain.Commands;
using SellingNewProduct.Domain.Customers;
using SellingNewProduct.Domain.Repositories;
using SellingNewProduct.Domain.ValueObjects;

namespace SellingNewProduct.Domain.Services;

internal sealed class CustomerService : ICustomerService
{
    private readonly ICustomerRepository myCustomerRepository;

    public CustomerService(ICustomerRepository theCustomerRepository)
    {
        myCustomerRepository = theCustomerRepository;
    }

    public Task<IReadOnlyList<Customer>> GetAllAsync(CancellationToken theCancellationToken = default)
        => myCustomerRepository.GetAllAsync(theCancellationToken);

    public Task<Customer?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default)
        => myCustomerRepository.GetByIdAsync(theId, theCancellationToken);

    public async Task<Customer> CreateAsync(CreateCustomerCommand theCommand, CancellationToken theCancellationToken = default)
    {
        var aAddress = Address.Create(
            theCommand.Street, theCommand.Ward, theCommand.District, theCommand.City, theCommand.Country);

        var aCustomer = Customer.Create(
            theCommand.FullName,
            Email.Create(theCommand.Email),
            theCommand.PhoneNumber,
            aAddress,
            theCommand.UserId);

        await myCustomerRepository.AddAsync(aCustomer, theCancellationToken);
        return aCustomer;
    }

    public Task DeleteAsync(Guid theId, CancellationToken theCancellationToken = default)
        => myCustomerRepository.DeleteAsync(theId, theCancellationToken);
}
