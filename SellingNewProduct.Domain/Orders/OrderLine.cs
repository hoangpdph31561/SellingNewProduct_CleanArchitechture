namespace SellingNewProduct.Domain.Orders;

/// <summary>
/// One requested line when placing an order: which product and how many. A Domain-owned input
/// for the order inbound port, so its signature does not depend on the application command type.
/// </summary>
public sealed record OrderLine(Guid ProductId, int Quantity);
