using SellingNewProduct.Domain.Users;

namespace SellingNewProduct.Domain.Repositories;

public interface IUserWriteRepository
{
    /// <summary>Loads a user aggregate (used by the employee-create rule "user must exist").</summary>
    Task<User?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default);

    Task AddAsync(User theUser, CancellationToken theCancellationToken = default);

    Task UpdateAsync(User theUser, CancellationToken theCancellationToken = default);
}
