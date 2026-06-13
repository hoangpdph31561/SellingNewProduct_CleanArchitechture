namespace SellingNewProduct.API.Contracts;

public sealed record CreateCustomerRequest(
    string FullName,
    string Email,
    string PhoneNumber,
    AddressDto Address,
    Guid? UserId);

public sealed record CustomerResponse(
    Guid Id,
    string FullName,
    string Email,
    string PhoneNumber,
    AddressDto Address,
    Guid? UserId,
    string Status);
