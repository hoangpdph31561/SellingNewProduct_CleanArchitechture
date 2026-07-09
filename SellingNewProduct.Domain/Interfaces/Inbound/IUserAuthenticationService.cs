using SellingNewProduct.Domain.Users;

namespace SellingNewProduct.Domain.Interfaces.Inbound;

/// <summary>
/// Inbound (driving) port for authenticating a login account: given a username and a raw password,
/// return the matching <see cref="User"/> when the credentials are valid. Keeping this in the Domain
/// means the credential-check rule lives with the aggregate, not in the API; token minting is a
/// separate outbound concern (<see cref="Outbound.IAccessTokenGenerator"/>).
/// </summary>
public interface IUserAuthenticationService
{
    /// <summary>
    /// Returns the user when the username exists and the password matches its stored hash;
    /// otherwise <c>null</c>. Deliberately does not distinguish "no such user" from "wrong
    /// password" so callers cannot use it to probe which usernames exist.
    /// </summary>
    Task<User?> AuthenticateAsync(string theUsername, string thePassword, CancellationToken theCancellationToken = default);
}
