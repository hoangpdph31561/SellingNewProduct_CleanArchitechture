using SellingNewProduct.Domain.Commands;
using SellingNewProduct.Domain.Orders;

namespace SellingNewProduct.Domain.Abstractions;

/// <summary>
/// Order write-side behavior (create + state transitions). The API depends on this,
/// not on the repositories. (Detail view / search are read side — see <c>IOrderQueries</c>.)
/// </summary>
public interface IOrderService
{
    Task<Order?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default);

    Task<Order> CreateAsync(CreateOrderCommand theCommand, CancellationToken theCancellationToken = default);

    Task<Order> AddDetailAsync(Guid theOrderId, Guid theProductId, int theQuantity, CancellationToken theCancellationToken = default);

    Task<Order> ConfirmAsync(Guid theOrderId, CancellationToken theCancellationToken = default);

    Task<Order> ShipAsync(Guid theOrderId, CancellationToken theCancellationToken = default);

    Task<Order> CancelAsync(Guid theOrderId, CancellationToken theCancellationToken = default);
}
