using SellingNewProduct.Domain.Interfaces.Inbound;
using SellingNewProduct.Domain.Interfaces.Outbound;
using SellingNewProduct.Domain.Users;

namespace SellingNewProduct.Domain.Services;

/// <summary>
/// Implements the authentication inbound port. Looks the account up by username through the read
/// repository, then defers the actual secret comparison to the <see cref="IPasswordHasher"/> port
/// so this service never sees the hashing algorithm and the plaintext never leaves the boundary.
/// </summary>
public sealed class UserAuthenticationService : IUserAuthenticationService
{
    private readonly IUserReadRepository myUserRepository;
    private readonly IPasswordHasher myPasswordHasher;

    public UserAuthenticationService(IUserReadRepository theUserRepository, IPasswordHasher thePasswordHasher)
    {
        myUserRepository = theUserRepository;
        myPasswordHasher = thePasswordHasher;
    }

    public async Task<User?> AuthenticateAsync(
        string theUsername,
        string thePassword,
        CancellationToken theCancellationToken = default)
    {
        var aUser = await myUserRepository.GetByUsernameAsync(theUsername, theCancellationToken);

        if (aUser is null || !myPasswordHasher.Verify(thePassword, aUser.PasswordHash))
        {
            return null;
        }

        return aUser;
    }
}
