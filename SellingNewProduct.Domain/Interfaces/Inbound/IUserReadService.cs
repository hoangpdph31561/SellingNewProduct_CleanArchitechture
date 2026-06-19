using SellingNewProduct.Domain.Users;

namespace SellingNewProduct.Domain.Interfaces.Inbound;

/// <summary>Inbound (driving) port for user reads.</summary>
public interface IUserReadService : IReadService<User>
{
    Task<IReadOnlyList<User>> GetAllAsync(CancellationToken theCancellationToken = default);

}
