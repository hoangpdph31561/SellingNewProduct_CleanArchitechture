namespace SellingNewProduct.Domain.Orders;

/// <summary>Lifecycle of an order.</summary>
public enum OrderStatus
{
    Draft = 1,
    Confirmed = 2,
    Shipped = 3,
    Cancelled = 4
}
