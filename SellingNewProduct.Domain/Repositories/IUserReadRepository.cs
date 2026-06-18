using SellingNewProduct.Domain.Users;

namespace SellingNewProduct.Domain.Repositories;

public interface IUserReadRepository
{
    Task<User?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default);

    Task<User?> GetByUsernameAsync(string theUsername, CancellationToken theCancellationToken = default);

    Task<IReadOnlyList<User>> GetAllAsync(CancellationToken theCancellationToken = default);
}
