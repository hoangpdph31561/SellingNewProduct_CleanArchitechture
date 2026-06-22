using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Orders;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Domain.Interfaces.Outbound;

public interface IOrderReadRepository : IReadRepository<Order>
{
    /// <summary>Loads the full order aggregate including its details (for the GET-by-id endpoint).</summary>

    Task<IReadOnlyList<Order>> GetByCustomerAsync(Guid theCustomerId, CancellationToken theCancellationToken = default);

    Task<IReadOnlyList<Order>> GetByDateRangeAsync(DateTime theFromUtc, DateTime theToUtc, CancellationToken theCancellationToken = default);

    Task<OrderDetailView?> GetOrderDetailAsync(Guid theOrderId, CancellationToken theCancellationToken = default);

    Task<CustomerOrderHistoryView?> GetCustomerHistoryAsync(Guid theCustomerId, CancellationToken theCancellationToken = default);

    Task<PagedResult<OrderSummaryView>> SearchAsync(OrderSearchQuery theQuery, CancellationToken theCancellationToken = default);

    Task<IReadOnlyList<OrderStatusCountView>> GetStatusBreakdownAsync(CancellationToken theCancellationToken = default);
}
