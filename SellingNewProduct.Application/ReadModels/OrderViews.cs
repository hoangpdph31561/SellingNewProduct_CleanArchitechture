namespace SellingNewProduct.Application.ReadModels;

/// <summary>
/// Flat read-model for ONE enriched order: includes customer name, selling
/// employee name and the amount already paid. This is NOT a domain aggregate —
/// it exists only for display (read side). That is why it lives in the
/// Application layer, not in Domain (Domain holds business rules only) and not
/// in API (Infrastructure must see this type to return it, but Infra does not
/// reference API).
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
