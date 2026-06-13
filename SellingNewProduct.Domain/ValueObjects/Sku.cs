using SellingNewProduct.Domain.Common;

namespace SellingNewProduct.Domain.ValueObjects;

/// <summary>
/// Stock keeping unit: the store's product code (e.g. TSHIRT-RED-M).
/// </summary>
public sealed class Sku : ValueObject
{
    public string Value { get; }

    private Sku(string theValue)
    {
        Value = theValue;
    }

    public static Sku Create(string theValue)
    {
        if (string.IsNullOrWhiteSpace(theValue))
        {
            throw new DomainException("Sku is required.");
        }

        return new Sku(theValue.Trim().ToUpperInvariant());
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
