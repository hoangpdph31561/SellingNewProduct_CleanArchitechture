using MediatR;
using SellingNewProduct.Domain.Employees;

namespace SellingNewProduct.Application.Employees;

/// <summary>Write-side command: create an employee linked to an existing user account.</summary>
public sealed record CreateEmployeeCommand(
    string FullName,
    string Position,
    DateTime HireDate,
    Guid UserId) : IRequest<Employee>;
