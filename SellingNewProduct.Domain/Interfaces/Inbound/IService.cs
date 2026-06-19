namespace SellingNewProduct.Domain.Interfaces.Inbound;

/// <summary>
/// Root marker for an inbound (driving) port. <typeparamref name="TEntity"/> is the aggregate
/// the service is about. It carries no members on purpose: write use-cases share no common
/// signature (place / create / confirm … all differ), so the only shared read operation lives in
/// <see cref="IReadService{TEntity}"/>; this base just ties the service family together.
/// </summary>
public interface IService<TEntity>
{
}
