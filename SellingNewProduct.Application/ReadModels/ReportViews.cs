namespace SellingNewProduct.Application.ReadModels;

/// <summary>
/// Best-selling product report: order lines grouped (GROUP BY) per product,
/// including the category name. Sourced from a JOIN of three tables
/// OrderDetails x Products x Categories (only Confirmed/Shipped orders count).
/// </summary>
public sealed record BestSellingProductView(
    Guid ProductId,
    string ProductName,
    string CategoryName,
    int TotalQuantitySold,
    decimal TotalRevenue);

/// <summary>
/// Sales per employee (leaderboard): orders grouped by the selling employee.
/// Sourced from a JOIN of Orders x Employees, GROUP BY employee
/// (only Confirmed/Shipped orders count — Draft and Cancelled are excluded).
/// </summary>
public sealed record EmployeeSalesView(
    Guid EmployeeId,
    string EmployeeName,
    string Position,
    int TotalOrders,
    decimal TotalRevenue);
