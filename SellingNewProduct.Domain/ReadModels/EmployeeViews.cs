namespace SellingNewProduct.Domain.ReadModels;

/// <summary>
/// Flat read-model for an employee list/search screen, enriched with the number of
/// real-sale orders the employee has handled (a correlated COUNT over Orders, counting
/// only Confirmed/Shipped). Read side only — not the Employee aggregate.
/// </summary>
public sealed record EmployeeSummaryView(
    Guid EmployeeId,
    string FullName,
    string Position,
    DateTime HireDate,
    int TotalOrders,
    string Status);
