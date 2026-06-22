namespace SellingNewProduct.Domain.Interfaces.Outbound;

/// <summary>
/// Read (query) side of a persistence port. Every entity repository can load a single aggregate
/// by id; aggregate-specific reads (list, search, summaries) are added by the concrete interface.
/// </summary>
public interface IReadRepository<TEntity> : IRepository<TEntity>
{
    /// <summary>Loads a single aggregate by id, or null when it does not exist.</summary>
    Task<TEntity?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default);
}
