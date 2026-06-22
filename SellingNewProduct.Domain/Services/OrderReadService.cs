using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Interfaces.Inbound;
using SellingNewProduct.Domain.Interfaces.Outbound;
using SellingNewProduct.Domain.Orders;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Domain.Services;

/// <summary>Implements the order read port; forwards to the read repository (no business rules).</summary>
public sealed class OrderReadService : IOrderReadService
{
    private readonly IOrderReadRepository myOrderRepository;

    public OrderReadService(IOrderReadRepository theOrderRepository)
    {
        myOrderRepository = theOrderRepository;
    }

    public Task<Order?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default)
        => myOrderRepository.GetByIdAsync(theId, theCancellationToken);

    public Task<OrderDetailView?> GetDetailAsync(Guid theId, CancellationToken theCancellationToken = default)
        => myOrderRepository.GetOrderDetailAsync(theId, theCancellationToken);

    public Task<CustomerOrderHistoryView?> GetCustomerHistoryAsync(Guid theCustomerId, CancellationToken theCancellationToken = default)
        => myOrderRepository.GetCustomerHistoryAsync(theCustomerId, theCancellationToken);

    public Task<PagedResult<OrderSummaryView>> SearchAsync(OrderSearchQuery theCriteria, CancellationToken theCancellationToken = default)
        => myOrderRepository.SearchAsync(theCriteria, theCancellationToken);

    public Task<IReadOnlyList<OrderStatusCountView>> GetStatusBreakdownAsync(CancellationToken theCancellationToken = default)
        => myOrderRepository.GetStatusBreakdownAsync(theCancellationToken);
}
