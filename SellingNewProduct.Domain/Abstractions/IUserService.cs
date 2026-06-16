using SellingNewProduct.Domain.Commands;
using SellingNewProduct.Domain.Users;

namespace SellingNewProduct.Domain.Abstractions;

/// <summary>User write-side behavior. The API depends on this, not on the repository.</summary>
public interface IUserService
{
    Task<IReadOnlyList<User>> GetAllAsync(CancellationToken theCancellationToken = default);

    Task<User?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default);

    Task<User> CreateAsync(CreateUserCommand theCommand, CancellationToken theCancellationToken = default);
}
