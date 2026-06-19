using MediatR;
using SellingNewProduct.Domain.Orders;

namespace SellingNewProduct.Application.Orders;

/// <summary>Write-side command: place a complete order in one call. Created as Draft.</summary>
public sealed record PlaceOrderCommand(
    Guid CustomerId,
    Guid EmployeeId,
    string Street,
    string Ward,
    string District,
    string City,
    string Country,
    IReadOnlyList<OrderItemCommand> Items) : IRequest<Order>;
