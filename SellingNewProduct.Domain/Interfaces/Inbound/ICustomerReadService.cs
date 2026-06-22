using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Customers;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Domain.Interfaces.Inbound;

/// <summary>Inbound (driving) port for customer reads.</summary>
public interface ICustomerReadService : IReadService<Customer>
{
    Task<IReadOnlyList<Customer>> GetAllAsync(CancellationToken theCancellationToken = default);

    Task<PagedResult<CustomerSummaryView>> SearchAsync(CustomerSearchQuery theCriteria, CancellationToken theCancellationToken = default);

    Task<PagedResult<TopCustomerView>> GetTopCustomersAsync(int thePage, int thePageSize, CancellationToken theCancellationToken = default);
}
