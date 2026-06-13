using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Users;
using SellingNewProduct.Domain.ValueObjects;
using SellingNewProduct.Infrastructure.MongoDB.Models;

namespace SellingNewProduct.Infrastructure.MongoDB.Mapping;

internal static class UserMapper
{
    public static UserDocument ToDocument(User theUser) => new()
    {
        Id = theUser.Id,
        Username = theUser.Username,
        PasswordHash = theUser.PasswordHash,
        Email = theUser.Email.Value,
        Role = (int)theUser.Role,
        Status = (int)theUser.Status,
        CreatedAtUtc = theUser.CreatedAtUtc,
        UpdatedAtUtc = theUser.UpdatedAtUtc
    };

    public static void MapInto(UserDocument theTarget, User theSource)
    {
        theTarget.Username = theSource.Username;
        theTarget.PasswordHash = theSource.PasswordHash;
        theTarget.Email = theSource.Email.Value;
        theTarget.Role = (int)theSource.Role;
        theTarget.Status = (int)theSource.Status;
        theTarget.UpdatedAtUtc = theSource.UpdatedAtUtc;
    }

    public static User ToDomain(UserDocument theDocument) => User.Rehydrate(
        theDocument.Id,
        theDocument.Username,
        theDocument.PasswordHash,
        Email.Create(theDocument.Email),
        (UserRole)theDocument.Role,
        (EntityStatus)theDocument.Status,
        theDocument.CreatedAtUtc,
        theDocument.UpdatedAtUtc);
}
