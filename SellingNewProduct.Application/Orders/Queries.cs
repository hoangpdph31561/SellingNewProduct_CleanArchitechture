using MediatR;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Orders;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;
using SellingNewProduct.Domain.Repositories;

namespace SellingNewProduct.Application.Orders;

public sealed record GetOrderByIdQuery(Guid Id) : IRequest<Order?>;

public sealed record GetOrderDetailViewQuery(Guid Id) : IRequest<OrderDetailView?>;

public sealed record GetCustomerOrderHistoryQuery(Guid CustomerId) : IRequest<CustomerOrderHistoryView?>;

public sealed record SearchOrdersQuery(OrderSearchQuery Criteria) : IRequest<PagedResult<OrderSummaryView>>;

public sealed record GetOrderStatusBreakdownQuery : IRequest<IReadOnlyList<OrderStatusCountView>>;

public sealed class OrderQueryHandlers :
    IRequestHandler<GetOrderByIdQuery, Order?>,
    IRequestHandler<GetOrderDetailViewQuery, OrderDetailView?>,
    IRequestHandler<GetCustomerOrderHistoryQuery, CustomerOrderHistoryView?>,
    IRequestHandler<SearchOrdersQuery, PagedResult<OrderSummaryView>>,
    IRequestHandler<GetOrderStatusBreakdownQuery, IReadOnlyList<OrderStatusCountView>>
{
    private readonly IOrderReadRepository myOrderRepository;

    public OrderQueryHandlers(IOrderReadRepository theOrderRepository)
    {
        myOrderRepository = theOrderRepository;
    }

    public Task<Order?> Handle(GetOrderByIdQuery theQuery, CancellationToken theCancellationToken)
        => myOrderRepository.GetByIdAsync(theQuery.Id, theCancellationToken);

    public Task<OrderDetailView?> Handle(GetOrderDetailViewQuery theQuery, CancellationToken theCancellationToken)
        => myOrderRepository.GetOrderDetailAsync(theQuery.Id, theCancellationToken);

    public Task<CustomerOrderHistoryView?> Handle(GetCustomerOrderHistoryQuery theQuery, CancellationToken theCancellationToken)
        => myOrderRepository.GetCustomerHistoryAsync(theQuery.CustomerId, theCancellationToken);

    public Task<PagedResult<OrderSummaryView>> Handle(SearchOrdersQuery theQuery, CancellationToken theCancellationToken)
        => myOrderRepository.SearchAsync(theQuery.Criteria, theCancellationToken);

    public Task<IReadOnlyList<OrderStatusCountView>> Handle(GetOrderStatusBreakdownQuery theQuery, CancellationToken theCancellationToken)
        => myOrderRepository.GetStatusBreakdownAsync(theCancellationToken);
}
