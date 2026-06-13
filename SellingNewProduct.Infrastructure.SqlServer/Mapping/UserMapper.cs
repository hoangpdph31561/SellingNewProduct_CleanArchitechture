using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Users;
using SellingNewProduct.Domain.ValueObjects;
using SellingNewProduct.Infrastructure.SqlServer.Models;

namespace SellingNewProduct.Infrastructure.SqlServer.Mapping;

/// <summary>Manual mapping between the User domain entity and its SQL record.</summary>
internal static class UserMapper
{
    public static UserRecord ToRecord(User theUser) => new()
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

    public static void MapInto(UserRecord theTarget, User theSource)
    {
        theTarget.Username = theSource.Username;
        theTarget.PasswordHash = theSource.PasswordHash;
        theTarget.Email = theSource.Email.Value;
        theTarget.Role = (int)theSource.Role;
        theTarget.Status = (int)theSource.Status;
        theTarget.UpdatedAtUtc = theSource.UpdatedAtUtc;
    }

    public static User ToDomain(UserRecord theRecord) => User.Rehydrate(
        theRecord.Id,
        theRecord.Username,
        theRecord.PasswordHash,
        Email.Create(theRecord.Email),
        (UserRole)theRecord.Role,
        (EntityStatus)theRecord.Status,
        theRecord.CreatedAtUtc,
        theRecord.UpdatedAtUtc);
}
