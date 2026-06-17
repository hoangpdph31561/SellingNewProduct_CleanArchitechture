using SellingNewProduct.Domain.Commands;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Orders;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Domain.Abstractions;

/// <summary>
/// Order behavior — the single entry point the API depends on for both the write side
/// (place a complete order + state transitions) and the read side (detail view, search, status
/// breakdown, and a customer's order history — all order data, enriched for display).
/// </summary>
public interface IOrderService
{
    Task<Order?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default);

    /// <summary>
    /// Places a complete order (customer/employee + shipping address + all line items) in one
    /// call, as Draft. Validates the customer/employee/products exist, that each product is
    /// active, and that there is enough stock for every line. Confirming is a separate step.
    /// </summary>
    Task<Order> PlaceAsync(PlaceOrderCommand theCommand, CancellationToken theCancellationToken = default);

    /// <summary>Confirms a draft order: reserves stock (decrements each product), then marks it Confirmed.</summary>
    Task<Order> ConfirmAsync(Guid theOrderId, CancellationToken theCancellationToken = default);

    Task<Order> ShipAsync(Guid theOrderId, CancellationToken theCancellationToken = default);

    Task<Order> CancelAsync(Guid theOrderId, CancellationToken theCancellationToken = default);

    /// <summary>One order enriched with customer/employee names, lines and amount paid, or <c>null</c>.</summary>
    Task<OrderDetailView?> GetOrderDetailAsync(Guid theOrderId, CancellationToken theCancellationToken = default);

    /// <summary>A customer's purchase history (total orders + total spent + order list), or <c>null</c> if the customer is not found.</summary>
    Task<CustomerOrderHistoryView?> GetCustomerHistoryAsync(Guid theCustomerId, CancellationToken theCancellationToken = default);

    /// <summary>Search/filter orders (see <see cref="OrderSearchQuery"/>), one page of enriched rows plus the total count.</summary>
    Task<PagedResult<OrderSummaryView>> SearchAsync(OrderSearchQuery theQuery, CancellationToken theCancellationToken = default);

    /// <summary>How many orders sit in each status and the total amount they represent (dashboard breakdown).</summary>
    Task<IReadOnlyList<OrderStatusCountView>> GetStatusBreakdownAsync(CancellationToken theCancellationToken = default);
}
