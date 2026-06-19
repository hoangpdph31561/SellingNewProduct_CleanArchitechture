using MediatR;
using SellingNewProduct.Domain.Orders;

namespace SellingNewProduct.Application.Orders;

public sealed record GetOrderByIdQuery(Guid Id) : IRequest<Order?>;
