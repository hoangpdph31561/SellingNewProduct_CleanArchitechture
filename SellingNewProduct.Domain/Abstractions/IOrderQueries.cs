using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Orders;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Domain.Abstractions;

/// <summary>
/// Read side for orders — kept separate from <c>IOrderRepository</c> (write side).
/// The repository returns the <see cref="Order"/> aggregate to run business logic;
/// the methods here return flat read-models that already JOIN in customer/employee
/// names, optimised for display (avoids the N+1 query problem). The interface lives
/// in Domain so the API depends only on Domain; Infrastructure provides the SQL/Mongo
/// implementation.
/// </summary>
public interface IOrderQueries
{
    /// <summary>
    /// Gets one order with the customer name, employee name, lines and amount paid.
    /// (SQL: JOIN Orders x Customers x Employees + SUM of Payments.)
    /// </summary>
    Task<OrderDetailView?> GetOrderDetailAsync(Guid theOrderId, CancellationToken theCancellationToken = default);

    /// <summary>
    /// A customer's purchase history: total orders + total spent + the order list
    /// (each with the selling employee's name). Returns <c>null</c> if the customer is not found.
    /// </summary>
    Task<CustomerOrderHistoryView?> GetCustomerHistoryAsync(Guid theCustomerId, CancellationToken theCancellationToken = default);

    /// <summary>
    /// Search/filter orders for a list screen — see <see cref="OrderSearchQuery"/> for the
    /// filters, paging and sorting. Returns ONE page of summary rows (already enriched with
    /// customer and employee names) plus the total count, so the screen can page without
    /// loading every matching order. Page/pageSize are clamped (see <see cref="PageRequest"/>).
    /// </summary>
    Task<PagedResult<OrderSummaryView>> SearchAsync(OrderSearchQuery theQuery, CancellationToken theCancellationToken = default);

    /// <summary>
    /// How many orders sit in each status and the total amount they represent — a
    /// GROUP BY OrderStatus over the Orders table, for a dashboard. Every status is
    /// returned (count 0 when none), ordered by the status value.
    /// </summary>
    Task<IReadOnlyList<OrderStatusCountView>> GetStatusBreakdownAsync(CancellationToken theCancellationToken = default);
}
