namespace SellingNewProduct.Domain.Interfaces.Outbound;

/// <summary>
/// Write (command) side of a persistence port. Every entity repository can add and update an
/// aggregate; loads-to-mutate, bulk writes and existence checks are added by the concrete
/// interface where the aggregate needs them.
/// </summary>
public interface IWriteRepository<TEntity> : IRepository<TEntity>
{
    Task AddAsync(TEntity theEntity, CancellationToken theCancellationToken = default);

    Task UpdateAsync(TEntity theEntity, CancellationToken theCancellationToken = default);
}
