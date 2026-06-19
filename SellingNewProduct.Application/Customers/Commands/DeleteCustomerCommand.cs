using MediatR;

namespace SellingNewProduct.Application.Customers;

public sealed record DeleteCustomerCommand(Guid Id) : IRequest;
