namespace SellingNewProduct.Domain.ReadModels;

/// <summary>
/// Flat read-model for ONE enriched order: includes customer name, selling
/// employee name and the amount already paid. This is NOT a domain aggregate —
/// it exists only for display (read side).
/// </summary>
public sealed record OrderDetailView(
    Guid OrderId,
    Guid CustomerId,
    string CustomerName,
    Guid EmployeeId,
    string EmployeeName,
    string OrderStatus,
    DateTime OrderDate,
    decimal TotalAmount,
    string Currency,
    decimal AmountPaid,
    IReadOnlyList<OrderLineView> Lines);

/// <summary>A single order line, flattened for display.</summary>
public sealed record OrderLineView(
    Guid OrderDetailId,
    Guid ProductId,
    string ProductName,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal);

/// <summary>
/// A customer's purchase history: total order count, total spent and the list
/// of orders (each carrying the name of the employee who sold it). Answers
/// "how many orders has this customer placed, and which ones".
/// </summary>
public sealed record CustomerOrderHistoryView(
    Guid CustomerId,
    string CustomerName,
    int TotalOrders,
    decimal TotalSpent,
    string Currency,
    IReadOnlyList<CustomerOrderItemView> Orders);

/// <summary>A single order inside a customer's purchase history.</summary>
public sealed record CustomerOrderItemView(
    Guid OrderId,
    DateTime OrderDate,
    string OrderStatus,
    string EmployeeName,
    decimal TotalAmount,
    string Currency);

/// <summary>
/// Summary row for an order list / search screen: only the columns needed to
/// list orders, enriched with customer and employee names (no need to load the
/// whole aggregate).
/// </summary>
public sealed record OrderSummaryView(
    Guid OrderId,
    string CustomerName,
    string EmployeeName,
    string OrderStatus,
    DateTime OrderDate,
    decimal TotalAmount,
    string Currency);

/// <summary>
/// How many orders sit in a given status, and the total amount they represent.
/// A GROUP BY OrderStatus over the Orders table — a dashboard tile (e.g. "3 Draft,
/// 12 Confirmed, 5 Shipped, 1 Cancelled").
/// </summary>
public sealed record OrderStatusCountView(
    string OrderStatus,
    int Count,
    decimal TotalAmount);
