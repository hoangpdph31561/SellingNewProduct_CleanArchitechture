using MediatR;
using SellingNewProduct.Domain.Orders;

namespace SellingNewProduct.Application.Orders;

public sealed record ShipOrderCommand(Guid Id) : IRequest<Order>;
