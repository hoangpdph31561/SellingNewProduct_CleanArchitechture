namespace SellingNewProduct.Domain.Interfaces.Inbound;

/// <summary>
/// Write (command) side of an inbound port. A marker: the write use-cases of each aggregate have
/// their own signatures (place / create / confirm / cancel …), so there is no shared method to
/// hoist here — the concrete interface declares them.
/// </summary>
public interface IWriteService<TEntity> : IService<TEntity>
{
}
