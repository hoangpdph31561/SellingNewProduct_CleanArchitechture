namespace SellingNewProduct.Domain.Commands;

/// <summary>
/// Input for <c>IOrderService.PlaceAsync</c>: places a complete order in ONE call — the
/// customer/employee, the shipping address (flat fields, no API types) and every line item.
/// The order is created as Draft; confirming it is a separate step. <see cref="Items"/> must
/// not be empty.
/// </summary>
public sealed record PlaceOrderCommand(
    Guid CustomerId,
    Guid EmployeeId,
    string Street,
    string Ward,
    string District,
    string City,
    string Country,
    IReadOnlyList<OrderItemCommand> Items);

/// <summary>One requested line of a <see cref="PlaceOrderCommand"/>.</summary>
public sealed record OrderItemCommand(Guid ProductId, int Quantity);
