using SellingNewProduct.Domain.Interfaces.Inbound;
using SellingNewProduct.Domain.Interfaces.Outbound;
using SellingNewProduct.Domain.Users;
using SellingNewProduct.Domain.ValueObjects;

namespace SellingNewProduct.Domain.Services;

/// <summary>
/// Implements the user inbound port. Hashes the raw password through the outbound
/// <see cref="IPasswordHasher"/> port before the aggregate ever stores it, so the plaintext
/// never reaches the persistence boundary.
/// </summary>
public sealed class UserWriteService : IUserWriteService
{
    private readonly IUserWriteRepository myUserRepository;
    private readonly IPasswordHasher myPasswordHasher;

    public UserWriteService(IUserWriteRepository theUserRepository, IPasswordHasher thePasswordHasher)
    {
        myUserRepository = theUserRepository;
        myPasswordHasher = thePasswordHasher;
    }

    /// <summary>Creates a user, hashing the supplied password.</summary>
    public async Task<User> CreateAsync(
        string theUsername,
        string thePassword,
        string theEmail,
        UserRole theRole,
        CancellationToken theCancellationToken = default)
    {
        var aUser = User.Create(
            theUsername,
            myPasswordHasher.Hash(thePassword),
            Email.Create(theEmail),
            theRole);

        await myUserRepository.AddAsync(aUser, theCancellationToken);
        return aUser;
    }
}
