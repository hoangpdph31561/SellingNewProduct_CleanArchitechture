using MediatR;
using SellingNewProduct.Domain.Employees;

namespace SellingNewProduct.Application.Employees;

public sealed record GetEmployeeByIdQuery(Guid Id) : IRequest<Employee?>;
