using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Orders;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Domain.Interfaces.Inbound;

/// <summary>Inbound (driving) port for order reads.</summary>
public interface IOrderReadService : IReadService<Order>
{
    Task<OrderDetailView?> GetDetailAsync(Guid theId, CancellationToken theCancellationToken = default);

    Task<CustomerOrderHistoryView?> GetCustomerHistoryAsync(Guid theCustomerId, CancellationToken theCancellationToken = default);

    Task<PagedResult<OrderSummaryView>> SearchAsync(OrderSearchQuery theCriteria, CancellationToken theCancellationToken = default);

    Task<IReadOnlyList<OrderStatusCountView>> GetStatusBreakdownAsync(CancellationToken theCancellationToken = default);
}
