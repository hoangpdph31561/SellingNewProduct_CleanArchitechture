using System.Security.Cryptography;
using System.Text;

namespace SellingNewProduct.API.Security;

/// <summary>
/// Minimal password hashing for the demo. NOT production grade — a real app
/// would use a salted, slow KDF such as PBKDF2 / bcrypt / Argon2.
/// </summary>
internal static class PasswordHasher
{
    public static string Hash(string thePassword)
    {
        var aBytes = SHA256.HashData(Encoding.UTF8.GetBytes(thePassword));
        return Convert.ToBase64String(aBytes);
    }
}
