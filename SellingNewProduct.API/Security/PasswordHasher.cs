using System.Security.Cryptography;
using System.Text;
using SellingNewProduct.Domain.Abstractions;

namespace SellingNewProduct.API.Security;

/// <summary>
/// Minimal password hashing for the demo. NOT production grade — a real app
/// would use a salted, slow KDF such as PBKDF2 / bcrypt / Argon2.
/// Implements the Domain's <see cref="IPasswordHasher"/> contract.
/// </summary>
internal sealed class PasswordHasher : IPasswordHasher
{
    public string Hash(string thePassword)
    {
        var aBytes = SHA256.HashData(Encoding.UTF8.GetBytes(thePassword));
        return Convert.ToBase64String(aBytes);
    }
}
