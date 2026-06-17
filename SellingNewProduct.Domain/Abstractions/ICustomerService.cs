using SellingNewProduct.Domain.Commands;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Customers;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Domain.Abstractions;

/// <summary>
/// Customer behavior — the single entry point the API depends on for both the write side
/// (aggregate operations) and the read side (list/search/top projections).
/// (A customer's order history is order data and lives on <c>IOrderService</c>.)
/// </summary>
public interface ICustomerService
{
    Task<IReadOnlyList<Customer>> GetAllAsync(CancellationToken theCancellationToken = default);

    Task<Customer?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default);

    Task<Customer> CreateAsync(CreateCustomerCommand theCommand, CancellationToken theCancellationToken = default);

    Task DeleteAsync(Guid theId, CancellationToken theCancellationToken = default);

    /// <summary>One enriched customer row, or <c>null</c> — the flat read-side counterpart of <see cref="GetByIdAsync"/>.</summary>
    Task<CustomerSummaryView?> GetSummaryByIdAsync(Guid theCustomerId, CancellationToken theCancellationToken = default);

    /// <summary>Search/filter customers (see <see cref="CustomerSearchQuery"/>), one page plus the total count.</summary>
    Task<PagedResult<CustomerSummaryView>> SearchAsync(CustomerSearchQuery theQuery, CancellationToken theCancellationToken = default);

    /// <summary>Customers ranked by total amount spent (real sales), one page plus the total number of paying customers.</summary>
    Task<PagedResult<TopCustomerView>> GetTopCustomersAsync(int thePage = 1, int thePageSize = 10, CancellationToken theCancellationToken = default);
}
