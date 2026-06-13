namespace SellingNewProduct.API.Contracts;

public sealed record CreateEmployeeRequest(string FullName, string Position, DateTime HireDate, Guid UserId);

public sealed record EmployeeResponse(Guid Id, string FullName, string Position, DateTime HireDate, Guid UserId, string Status);
