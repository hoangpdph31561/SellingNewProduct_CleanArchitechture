namespace SellingNewProduct.API.Contracts;

/// <summary>Address payload reused by several requests/responses.</summary>
public sealed record AddressDto(
    string Street,
    string Ward,
    string District,
    string City,
    string Country);
