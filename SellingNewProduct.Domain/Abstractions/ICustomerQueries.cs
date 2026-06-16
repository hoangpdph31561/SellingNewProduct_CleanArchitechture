using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Domain.Abstractions;

/// <summary>
/// Read side for customers — kept separate from <c>ICustomerRepository</c> (write side).
/// The repository returns the Customer aggregate for business logic; these methods return
/// flat read-models for list screens and reports. The interface lives in Domain so the API
/// depends only on Domain; Infrastructure provides the SQL/Mongo implementation.
/// </summary>
public interface ICustomerQueries
{
    /// <summary>One enriched customer row for display, or <c>null</c> if not found.</summary>
    Task<CustomerSummaryView?> GetByIdAsync(Guid theCustomerId, CancellationToken theCancellationToken = default);

    /// <summary>
    /// Search/filter customers for a list screen — see <see cref="CustomerSearchQuery"/> for
    /// the filters, paging and sorting. Soft-deleted rows are never returned. Returns one page
    /// plus the total count.
    /// </summary>
    Task<PagedResult<CustomerSummaryView>> SearchAsync(CustomerSearchQuery theQuery, CancellationToken theCancellationToken = default);

    /// <summary>
    /// Customers ranked by total amount spent (real sales only), returned one page at a
    /// time plus the total number of paying customers. Page 1 is the classic "top N".
    /// (SQL: JOIN Orders x Customers, GROUP BY customer.)
    /// </summary>
    Task<PagedResult<TopCustomerView>> GetTopCustomersAsync(
        int thePage = 1,
        int thePageSize = 10,
        CancellationToken theCancellationToken = default);
}
