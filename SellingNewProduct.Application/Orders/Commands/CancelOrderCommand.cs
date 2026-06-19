using MediatR;
using SellingNewProduct.Domain.Orders;

namespace SellingNewProduct.Application.Orders;

public sealed record CancelOrderCommand(Guid Id) : IRequest<Order>;
