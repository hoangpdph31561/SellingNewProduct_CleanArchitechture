using MediatR;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Application.Orders;

public sealed record GetCustomerOrderHistoryQuery(Guid CustomerId) : IRequest<CustomerOrderHistoryView?>;
