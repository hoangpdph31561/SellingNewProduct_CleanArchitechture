using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Customers;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Domain.Repositories;

public interface ICustomerReadRepository
{
    Task<Customer?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default);

    Task<IReadOnlyList<Customer>> GetAllAsync(CancellationToken theCancellationToken = default);

    Task<CustomerSummaryView?> GetSummaryByIdAsync(Guid theCustomerId, CancellationToken theCancellationToken = default);

    Task<PagedResult<CustomerSummaryView>> SearchAsync(CustomerSearchQuery theQuery, CancellationToken theCancellationToken = default);

    Task<PagedResult<TopCustomerView>> GetTopCustomersAsync(int thePage = 1, int thePageSize = 10, CancellationToken theCancellationToken = default);
}
