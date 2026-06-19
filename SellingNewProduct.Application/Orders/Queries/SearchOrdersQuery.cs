using MediatR;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Application.Orders;

public sealed record SearchOrdersQuery(OrderSearchQuery Criteria) : IRequest<PagedResult<OrderSummaryView>>;
