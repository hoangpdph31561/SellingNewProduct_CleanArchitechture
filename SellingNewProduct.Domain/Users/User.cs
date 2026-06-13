using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.ValueObjects;

namespace SellingNewProduct.Domain.Users;

/// <summary>
/// A login account. Aggregate root. Password is stored only as a hash;
/// the domain never deals with plain-text passwords.
/// </summary>
public sealed class User : AggregateRoot<Guid>
{
    public string Username { get; private set; } = default!;

    public string PasswordHash { get; private set; } = default!;

    public Email Email { get; private set; } = default!;

    public UserRole Role { get; private set; }

    private User()
    {
    }

    private User(Guid theId, string theUsername, string thePasswordHash, Email theEmail, UserRole theRole)
        : base(theId)
    {
        Username = theUsername;
        PasswordHash = thePasswordHash;
        Email = theEmail;
        Role = theRole;
    }

    public static User Create(string theUsername, string thePasswordHash, Email theEmail, UserRole theRole)
    {
        if (string.IsNullOrWhiteSpace(theUsername))
        {
            throw new DomainException("Username is required.");
        }

        if (string.IsNullOrWhiteSpace(thePasswordHash))
        {
            throw new DomainException("Password hash is required.");
        }

        return new User(Guid.NewGuid(), theUsername.Trim(), thePasswordHash, theEmail, theRole);
    }

    public void ChangePassword(string theNewPasswordHash)
    {
        if (string.IsNullOrWhiteSpace(theNewPasswordHash))
        {
            throw new DomainException("Password hash is required.");
        }

        PasswordHash = theNewPasswordHash;
        MarkUpdated();
    }

    public void ChangeEmail(Email theNewEmail)
    {
        Email = theNewEmail;
        MarkUpdated();
    }

    public void ChangeRole(UserRole theNewRole)
    {
        Role = theNewRole;
        MarkUpdated();
    }

    /// <summary>Reconstruct an existing user from persistence without running business rules.</summary>
    public static User Rehydrate(
        Guid theId,
        string theUsername,
        string thePasswordHash,
        Email theEmail,
        UserRole theRole,
        EntityStatus theStatus,
        DateTime theCreatedAtUtc,
        DateTime? theUpdatedAtUtc)
    {
        var aUser = new User
        {
            Username = theUsername,
            PasswordHash = thePasswordHash,
            Email = theEmail,
            Role = theRole
        };

        aUser.RestoreState(theId, theStatus, theCreatedAtUtc, theUpdatedAtUtc);
        return aUser;
    }
}
