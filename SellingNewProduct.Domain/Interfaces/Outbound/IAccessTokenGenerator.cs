using SellingNewProduct.Domain.Users;

namespace SellingNewProduct.Domain.Interfaces.Outbound;

/// <summary>
/// Mints an access token for an authenticated <see cref="User"/>. The Domain owns this contract
/// so the authentication service can hand a caller a token without knowing the token format or
/// signing key; the concrete implementation (JWT here) is provided by an outer layer via DI,
/// mirroring how <see cref="IPasswordHasher"/> is wired in.
/// </summary>
public interface IAccessTokenGenerator
{
    AccessToken Generate(User theUser);
}
