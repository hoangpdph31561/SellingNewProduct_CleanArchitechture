using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Orders;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Domain.Repositories;

public interface IOrderRepository
{
    /// <summary>Loads the full order aggregate including its details.</summary>
    Task<Order?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default);

    Task<IReadOnlyList<Order>> GetByCustomerAsync(Guid theCustomerId, CancellationToken theCancellationToken = default);

    Task<IReadOnlyList<Order>> GetByDateRangeAsync(DateTime theFromUtc, DateTime theToUtc, CancellationToken theCancellationToken = default);

    Task AddAsync(Order theOrder, CancellationToken theCancellationToken = default);

    Task UpdateAsync(Order theOrder, CancellationToken theCancellationToken = default);

    // Read side: order detail/search/status breakdown + a customer's order history, all
    // enriched with customer/employee names (read models, not the aggregate).
    Task<OrderDetailView?> GetOrderDetailAsync(Guid theOrderId, CancellationToken theCancellationToken = default);

    Task<CustomerOrderHistoryView?> GetCustomerHistoryAsync(Guid theCustomerId, CancellationToken theCancellationToken = default);

    Task<PagedResult<OrderSummaryView>> SearchAsync(OrderSearchQuery theQuery, CancellationToken theCancellationToken = default);

    Task<IReadOnlyList<OrderStatusCountView>> GetStatusBreakdownAsync(CancellationToken theCancellationToken = default);
}
