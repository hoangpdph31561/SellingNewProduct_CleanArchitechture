namespace SellingNewProduct.API.Contracts;

public sealed record CreateOrderRequest(Guid CustomerId, Guid EmployeeId, AddressDto ShippingAddress);

public sealed record AddOrderDetailRequest(Guid ProductId, int Quantity);

public sealed record OrderDetailResponse(
    Guid Id,
    Guid ProductId,
    string ProductName,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal);

public sealed record OrderResponse(
    Guid Id,
    Guid CustomerId,
    Guid EmployeeId,
    string OrderStatus,
    DateTime OrderDate,
    AddressDto ShippingAddress,
    decimal TotalAmount,
    string Currency,
    IReadOnlyList<OrderDetailResponse> Details,
    string Status);
