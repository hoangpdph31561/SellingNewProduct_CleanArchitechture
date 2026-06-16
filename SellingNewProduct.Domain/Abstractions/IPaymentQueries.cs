using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Domain.Abstractions;

/// <summary>
/// Read side for payments — kept separate from <c>IPaymentRepository</c> (write side).
/// Covers a payment list/search screen and the "orders that still owe money" report,
/// both of which are query results rather than properties of any single aggregate.
/// </summary>
public interface IPaymentQueries
{
    /// <summary>
    /// Search/filter payments for a list screen — see <see cref="PaymentSearchQuery"/> for the
    /// filters, paging and sorting. Soft-deleted rows are never returned. Returns one page plus
    /// the total count.
    /// </summary>
    Task<PagedResult<PaymentSummaryView>> SearchAsync(PaymentSearchQuery theQuery, CancellationToken theCancellationToken = default);

    /// <summary>
    /// Orders that are not yet fully paid: order total minus the SUM of completed payments,
    /// enriched with the customer name. Only orders with a positive outstanding balance are
    /// returned, biggest balance first. (Draft/Cancelled orders are excluded.) Returns one
    /// page plus the total count of outstanding orders.
    /// </summary>
    Task<PagedResult<OutstandingOrderView>> GetOutstandingOrdersAsync(
        int thePage = 1,
        int thePageSize = PageRequest.DefaultPageSize,
        CancellationToken theCancellationToken = default);
}
