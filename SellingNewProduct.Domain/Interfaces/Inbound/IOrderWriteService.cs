using SellingNewProduct.Domain.Orders;
using SellingNewProduct.Domain.ValueObjects;

namespace SellingNewProduct.Domain.Interfaces.Inbound;

/// <summary>
/// Inbound (driving) port for the order lifecycle. Driving adapters — the application's MediatR
/// command handlers — call this; the Domain service implements it. Holds the rules that span
/// the Order and Product aggregates.
/// </summary>
public interface IOrderWriteService : IWriteService<Order>
{
    /// <summary>Places a complete order in one call. The order is created as Draft.</summary>
    Task<Order> PlaceAsync(
        Guid theCustomerId,
        Guid theEmployeeId,
        Address theShippingAddress,
        IReadOnlyList<OrderLine> theLines,
        CancellationToken theCancellationToken = default);

    /// <summary>Confirms a draft order and reserves stock (atomically).</summary>
    Task<Order> ConfirmAsync(Guid theOrderId, CancellationToken theCancellationToken = default);

    /// <summary>Marks a confirmed order as shipped.</summary>
    Task<Order> ShipAsync(Guid theOrderId, CancellationToken theCancellationToken = default);

    /// <summary>Cancels an order, returning reserved stock when it had been confirmed.</summary>
    Task<Order> CancelAsync(Guid theOrderId, CancellationToken theCancellationToken = default);
}
