using SellingNewProduct.Domain.Users;

namespace SellingNewProduct.Domain.Interfaces.Inbound;

/// <summary>
/// Inbound (driving) port for user creation. Hashes the raw password through the outbound
/// password-hasher port before the aggregate ever stores it.
/// </summary>
public interface IUserWriteService : IWriteService<User>
{
    /// <summary>Creates a user, hashing the supplied password.</summary>
    Task<User> CreateAsync(
        string theUsername,
        string thePassword,
        string theEmail,
        UserRole theRole,
        CancellationToken theCancellationToken = default);
}
