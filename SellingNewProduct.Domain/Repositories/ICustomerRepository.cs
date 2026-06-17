using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Customers;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Domain.Repositories;

public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default);

    Task<IReadOnlyList<Customer>> GetAllAsync(CancellationToken theCancellationToken = default);

    Task AddAsync(Customer theCustomer, CancellationToken theCancellationToken = default);

    Task UpdateAsync(Customer theCustomer, CancellationToken theCancellationToken = default);

    Task DeleteAsync(Guid theId, CancellationToken theCancellationToken = default);

    // Read side: flat customer rows + the "top customers" report (read models, not the aggregate).
    Task<CustomerSummaryView?> GetSummaryByIdAsync(Guid theCustomerId, CancellationToken theCancellationToken = default);

    Task<PagedResult<CustomerSummaryView>> SearchAsync(CustomerSearchQuery theQuery, CancellationToken theCancellationToken = default);

    Task<PagedResult<TopCustomerView>> GetTopCustomersAsync(int thePage = 1, int thePageSize = 10, CancellationToken theCancellationToken = default);
}
