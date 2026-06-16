namespace SellingNewProduct.Domain.ReadModels;

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

/// <summary>
/// Sales per category: order lines grouped by the product's category.
/// Sourced from OrderDetails x Products x Categories x Orders, GROUP BY category
/// (only Confirmed/Shipped orders count).
/// </summary>
public sealed record CategorySalesView(
    Guid CategoryId,
    string CategoryName,
    int TotalQuantitySold,
    decimal TotalRevenue);

/// <summary>
/// Revenue for a single day: the number of real-sale orders and their total amount.
/// Sourced from Orders grouped by the DATE part of OrderDate (only Confirmed/Shipped).
/// A time series for a sales-over-time chart.
/// </summary>
public sealed record DailySalesView(
    DateTime Date,
    int TotalOrders,
    decimal TotalRevenue);

/// <summary>
/// A product whose stock has fallen to or below a threshold, with its category name —
/// a restock-alert row. Sourced from Products x Categories. Read side only.
/// </summary>
public sealed record LowStockProductView(
    Guid ProductId,
    string ProductName,
    string CategoryName,
    int StockQuantity);
