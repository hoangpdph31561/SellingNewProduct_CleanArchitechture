using MediatR;
using SellingNewProduct.Domain.Employees;

namespace SellingNewProduct.Application.Employees;

public sealed record GetAllEmployeesQuery : IRequest<IReadOnlyList<Employee>>;
