using System.Text.RegularExpressions;
using SellingNewProduct.Domain.Common;

namespace SellingNewProduct.Domain.ValueObjects;

/// <summary>
/// A validated email address. It is impossible to create an invalid Email.
/// </summary>
public sealed partial class Email : ValueObject
{
    public string Value { get; }

    private Email(string theValue)
    {
        Value = theValue;
    }

    public static Email Create(string theValue)
    {
        if (string.IsNullOrWhiteSpace(theValue))
        {
            throw new DomainException("Email is required.");
        }

        var aNormalized = theValue.Trim().ToLowerInvariant();

        if (!EmailRegex().IsMatch(aNormalized))
        {
            throw new DomainException($"'{theValue}' is not a valid email address.");
        }

        return new Email(aNormalized);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailRegex();
}
