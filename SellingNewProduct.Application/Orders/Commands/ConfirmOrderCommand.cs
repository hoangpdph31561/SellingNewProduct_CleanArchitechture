using MediatR;
using SellingNewProduct.Domain.Orders;

namespace SellingNewProduct.Application.Orders;

public sealed record ConfirmOrderCommand(Guid Id) : IRequest<Order>;
