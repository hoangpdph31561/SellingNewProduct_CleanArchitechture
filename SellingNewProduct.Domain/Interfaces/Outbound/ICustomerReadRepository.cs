using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Customers;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Domain.Interfaces.Outbound;

public interface ICustomerReadRepository : IReadRepository<Customer>
{
    Task<IReadOnlyList<Customer>> GetAllAsync(CancellationToken theCancellationToken = default);

    Task<CustomerSummaryView?> GetSummaryByIdAsync(Guid theCustomerId, CancellationToken theCancellationToken = default);

    Task<PagedResult<CustomerSummaryView>> SearchAsync(CustomerSearchQuery theQuery, CancellationToken theCancellationToken = default);

    Task<PagedResult<TopCustomerView>> GetTopCustomersAsync(int thePage = 1, int thePageSize = 10, CancellationToken theCancellationToken = default);
}
