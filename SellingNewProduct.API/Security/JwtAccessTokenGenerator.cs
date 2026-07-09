using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SellingNewProduct.Domain.Interfaces.Outbound;
using SellingNewProduct.Domain.Users;

namespace SellingNewProduct.API.Security;

/// <summary>
/// API-layer implementation of the Domain <see cref="IAccessTokenGenerator"/> contract: encodes an
/// authenticated user as a signed JWT (HS256). The user's <see cref="UserRole"/> is written as the
/// standard role claim so ASP.NET Core's [Authorize(Roles = ...)] can enforce it out of the box.
/// </summary>
internal sealed class JwtAccessTokenGenerator : IAccessTokenGenerator
{
    private readonly JwtOptions myOptions;

    public JwtAccessTokenGenerator(IOptions<JwtOptions> theOptions)
    {
        myOptions = theOptions.Value;
    }

    public AccessToken Generate(User theUser)
    {
        var aExpiresAtUtc = DateTime.UtcNow.AddMinutes(myOptions.AccessTokenMinutes);

        var aClaims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, theUser.Id.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, theUser.Username),
            new(JwtRegisteredClaimNames.Email, theUser.Email.Value),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.Role, theUser.Role.ToString())
        };

        var aSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(myOptions.SigningKey));
        var aCredentials = new SigningCredentials(aSigningKey, SecurityAlgorithms.HmacSha256);

        var aDescriptor = new SecurityTokenDescriptor
        {
            Issuer = myOptions.Issuer,
            Audience = myOptions.Audience,
            Subject = new ClaimsIdentity(aClaims),
            Expires = aExpiresAtUtc,
            SigningCredentials = aCredentials
        };

        var aToken = new JsonWebTokenHandler().CreateToken(aDescriptor);
        return new AccessToken(aToken, aExpiresAtUtc);
    }
}
