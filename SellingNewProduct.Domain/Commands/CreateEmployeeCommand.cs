namespace SellingNewProduct.Domain.Commands;

/// <summary>Input for <c>IEmployeeService.CreateAsync</c>.</summary>
public sealed record CreateEmployeeCommand(
    string FullName,
    string Position,
    DateTime HireDate,
    Guid UserId);
