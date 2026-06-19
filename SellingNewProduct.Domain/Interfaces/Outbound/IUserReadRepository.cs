using SellingNewProduct.Domain.Users;

namespace SellingNewProduct.Domain.Interfaces.Outbound;

public interface IUserReadRepository : IReadRepository<User>
{
    Task<User?> GetByUsernameAsync(string theUsername, CancellationToken theCancellationToken = default);

    Task<IReadOnlyList<User>> GetAllAsync(CancellationToken theCancellationToken = default);
}
