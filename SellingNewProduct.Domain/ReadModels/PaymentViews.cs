namespace SellingNewProduct.Domain.ReadModels;

/// <summary>
/// Flat read-model for a payment list/search screen. The method and status are exposed
/// as their enum NAMES (not the stored ints) so the screen does not need the enum.
/// Read side only — not the Payment aggregate.
/// </summary>
public sealed record PaymentSummaryView(
    Guid PaymentId,
    Guid OrderId,
    decimal Amount,
    string Currency,
    string Method,
    string PaymentStatus,
    DateTime? PaidAtUtc);

/// <summary>
/// An order that still owes money: the order total, how much has been paid (SUM of the
/// order's completed payments) and the remaining balance. Answers "who still owes us".
/// Sourced from Orders x Customers x Payments. Read side only.
/// </summary>
public sealed record OutstandingOrderView(
    Guid OrderId,
    string CustomerName,
    decimal TotalAmount,
    decimal AmountPaid,
    decimal Outstanding,
    string Currency);
