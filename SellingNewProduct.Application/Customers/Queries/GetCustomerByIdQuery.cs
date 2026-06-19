using MediatR;
using SellingNewProduct.Domain.Customers;

namespace SellingNewProduct.Application.Customers;

public sealed record GetCustomerByIdQuery(Guid Id) : IRequest<Customer?>;
