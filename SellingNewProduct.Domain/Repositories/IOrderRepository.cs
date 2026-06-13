using SellingNewProduct.Domain.Orders;

namespace SellingNewProduct.Domain.Repositories;

public interface IOrderRepository
{
    /// <summary>Loads the full order aggregate including its details.</summary>
    Task<Order?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default);

    Task<IReadOnlyList<Order>> GetByCustomerAsync(Guid theCustomerId, CancellationToken theCancellationToken = default);

    Task<IReadOnlyList<Order>> GetByDateRangeAsync(DateTime theFromUtc, DateTime theToUtc, CancellationToken theCancellationToken = default);

    Task AddAsync(Order theOrder, CancellationToken theCancellationToken = default);

    Task UpdateAsync(Order theOrder, CancellationToken theCancellationToken = default);
}
