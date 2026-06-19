using MediatR;
using SellingNewProduct.Domain.Customers;

namespace SellingNewProduct.Application.Customers;

public sealed record GetAllCustomersQuery : IRequest<IReadOnlyList<Customer>>;
