namespace SellingNewProduct.Domain.Users;

/// <summary>
/// A minted access token plus the instant it expires. The Domain treats the token as an opaque
/// string — it neither knows nor cares that the outer layer happens to encode it as a JWT.
/// </summary>
public sealed record AccessToken(string Value, DateTime ExpiresAtUtc);
