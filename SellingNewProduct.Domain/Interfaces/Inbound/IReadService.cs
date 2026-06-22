namespace SellingNewProduct.Domain.Interfaces.Inbound;

/// <summary>
/// Read (query) side of an inbound port. Every entity read service can fetch a single aggregate
/// by id; aggregate-specific reads (list, search, summaries) are added by the concrete interface.
/// </summary>
public interface IReadService<TEntity> : IService<TEntity>
{
    /// <summary>Fetches a single aggregate by id, or null when it does not exist.</summary>
    Task<TEntity?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default);
}
