using SellingNewProduct.Application.Common;
using SellingNewProduct.Application.ReadModels;
using SellingNewProduct.Domain.Orders;

namespace SellingNewProduct.Application.Queries;

/// <summary>
/// Read side for orders — kept separate from <c>IOrderRepository</c> (write side).
/// The repository returns the <see cref="Order"/> aggregate to run business logic;
/// the methods here return flat read-models that already JOIN in customer/employee
/// names, optimised for display (avoids the N+1 query problem).
///
/// Why does this interface live in Application instead of Domain?
/// Because a read-model is not a business rule. Domain keeps only business
/// invariants. Stitching several tables together for display is a concern of the
/// application/read layer.
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
    /// Search/filter orders for a list screen. Every filter is optional (null = no filter):
    /// by customer/employee id, by a case-insensitive "contains" on the customer or employee
    /// NAME, by order status, and by an order-date range. Returns ONE page of summary rows
    /// (already enriched with customer and employee names) plus the total count, so the screen
    /// can show paging without loading every matching order. <paramref name="thePage"/> is
    /// 1-based; out-of-range page/pageSize values are clamped (see <see cref="PageRequest"/>).
    /// <paramref name="theSortBy"/> accepts <c>orderDate</c> (default, newest first),
    /// <c>totalAmount</c>, <c>customerName</c> or <c>employeeName</c>.
    /// </summary>
    Task<PagedResult<OrderSummaryView>> SearchAsync(
        Guid? theCustomerId = null,
        Guid? theEmployeeId = null,
        string? theCustomerName = null,
        string? theEmployeeName = null,
        OrderStatus? theStatus = null,
        DateTime? theFromUtc = null,
        DateTime? theToUtc = null,
        int thePage = 1,
        int thePageSize = PageRequest.DefaultPageSize,
        string? theSortBy = null,
        bool theSortDescending = false,
        CancellationToken theCancellationToken = default);
}
