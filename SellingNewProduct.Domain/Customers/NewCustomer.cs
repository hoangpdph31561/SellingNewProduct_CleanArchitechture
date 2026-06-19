namespace SellingNewProduct.Domain.Customers;

/// <summary>
/// A request to create one customer. A Domain-owned input for the customer inbound port, so its
/// signature does not depend on the application layer's command type. Address is carried as flat
/// fields; the service composes the value objects.
/// </summary>
public sealed record NewCustomer(
    string FullName,
    string Email,
    string PhoneNumber,
    string Street,
    string Ward,
    string District,
    string City,
    string Country,
    Guid? UserId);
