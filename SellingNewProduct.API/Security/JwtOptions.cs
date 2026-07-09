namespace SellingNewProduct.API.Security;

/// <summary>
/// Bearer-token settings bound from the "Jwt" configuration section. The signing key is a shared
/// secret (symmetric HS256) — keep it out of source control in a real app (user-secrets / env vars).
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = default!;

    public string Audience { get; set; } = default!;

    /// <summary>Symmetric signing secret. Must be at least 32 bytes for HS256.</summary>
    public string SigningKey { get; set; } = default!;

    /// <summary>How long an issued token stays valid.</summary>
    public int AccessTokenMinutes { get; set; } = 60;
}
