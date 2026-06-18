using SellingNewProduct.Domain.Orders;

namespace SellingNewProduct.Domain.Repositories;

public interface IOrderWriteRepository
{
    /// <summary>Loads the full order aggregate (including details) to mutate it.</summary>
    Task<Order?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default);

    Task AddAsync(Order theOrder, CancellationToken theCancellationToken = default);

    Task UpdateAsync(Order theOrder, CancellationToken theCancellationToken = default);
}
