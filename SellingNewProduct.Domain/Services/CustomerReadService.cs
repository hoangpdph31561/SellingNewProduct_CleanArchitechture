using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Customers;
using SellingNewProduct.Domain.Interfaces.Inbound;
using SellingNewProduct.Domain.Interfaces.Outbound;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Domain.Services;

/// <summary>Implements the customer read port; forwards to the read repository (no business rules).</summary>
public sealed class CustomerReadService : ICustomerReadService
{
    private readonly ICustomerReadRepository myCustomerRepository;

    public CustomerReadService(ICustomerReadRepository theCustomerRepository)
    {
        myCustomerRepository = theCustomerRepository;
    }

    public Task<IReadOnlyList<Customer>> GetAllAsync(CancellationToken theCancellationToken = default)
        => myCustomerRepository.GetAllAsync(theCancellationToken);

    public Task<Customer?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default)
        => myCustomerRepository.GetByIdAsync(theId, theCancellationToken);

    public Task<PagedResult<CustomerSummaryView>> SearchAsync(CustomerSearchQuery theCriteria, CancellationToken theCancellationToken = default)
        => myCustomerRepository.SearchAsync(theCriteria, theCancellationToken);

    public Task<PagedResult<TopCustomerView>> GetTopCustomersAsync(int thePage, int thePageSize, CancellationToken theCancellationToken = default)
        => myCustomerRepository.GetTopCustomersAsync(thePage, thePageSize, theCancellationToken);
}
