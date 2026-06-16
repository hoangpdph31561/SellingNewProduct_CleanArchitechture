namespace SellingNewProduct.Domain.ReadModels;

/// <summary>
/// Flat read-model for a customer list/search screen. Only the columns a grid needs;
/// the full address is collapsed to the city. Read side only — not the Customer aggregate.
/// </summary>
public sealed record CustomerSummaryView(
    Guid CustomerId,
    string FullName,
    string Email,
    string PhoneNumber,
    string City,
    string Status);

/// <summary>
/// A customer ranked by how much they have spent: the number of real-sale orders and
/// the total amount. Sourced from a JOIN of Customers x Orders, GROUP BY customer
/// (only Confirmed/Shipped orders count — Draft and Cancelled are excluded).
/// </summary>
public sealed record TopCustomerView(
    Guid CustomerId,
    string FullName,
    int TotalOrders,
    decimal TotalSpent,
    string Currency);
