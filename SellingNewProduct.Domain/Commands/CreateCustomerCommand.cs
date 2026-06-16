namespace SellingNewProduct.Domain.Commands;

/// <summary>
/// Input for <c>ICustomerService.CreateAsync</c>. The address is kept as flat fields here
/// (the service turns them into the <c>Address</c> value object) so the command stays a
/// simple data carrier with no dependency on API DTOs.
/// </summary>
public sealed record CreateCustomerCommand(
    string FullName,
    string Email,
    string PhoneNumber,
    string Street,
    string Ward,
    string District,
    string City,
    string Country,
    Guid? UserId);
