namespace SellingNewProduct.Domain.Commands;

/// <summary>
/// Input for <c>IOrderService.CreateAsync</c>. The shipping address is kept as flat fields
/// (the service builds the <c>Address</c> value object) so the command carries no API types.
/// </summary>
public sealed record CreateOrderCommand(
    Guid CustomerId,
    Guid EmployeeId,
    string Street,
    string Ward,
    string District,
    string City,
    string Country);
