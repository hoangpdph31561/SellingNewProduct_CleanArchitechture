using SellingNewProduct.Domain.Orders;

namespace SellingNewProduct.Domain.Interfaces.Outbound;

public interface IOrderWriteRepository : IWriteRepository<Order>
{
    /// <summary>Loads the full order aggregate (including details) to mutate it.</summary>
    Task<Order?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default);

}
