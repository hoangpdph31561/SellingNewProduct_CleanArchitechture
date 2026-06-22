namespace SellingNewProduct.Domain.Interfaces.Outbound;

/// <summary>
/// Root marker for a persistence (driven) port. <typeparamref name="TEntity"/> is the aggregate
/// the repository persists. It carries no members on purpose: read and write sides share almost
/// no operation, so the common contract lives in <see cref="IReadRepository{TEntity}"/> and
/// <see cref="IWriteRepository{TEntity}"/>; this base only ties the family together for generic
/// constraints and discoverability.
/// </summary>
public interface IRepository<TEntity>
{
}
