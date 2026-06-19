namespace SellingNewProduct.Application.Orders;

/// <summary>One requested line of a <see cref="PlaceOrderCommand"/>.</summary>
public sealed record OrderItemCommand(Guid ProductId, int Quantity);
