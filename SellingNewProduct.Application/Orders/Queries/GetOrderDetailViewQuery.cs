using MediatR;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Application.Orders;

public sealed record GetOrderDetailViewQuery(Guid Id) : IRequest<OrderDetailView?>;
