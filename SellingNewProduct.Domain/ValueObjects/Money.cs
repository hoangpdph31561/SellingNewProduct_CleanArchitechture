using SellingNewProduct.Domain.Common;

namespace SellingNewProduct.Domain.ValueObjects;

/// <summary>
/// An amount of money in a given currency. Immutable. Amount can never be negative.
/// </summary>
public sealed class Money : ValueObject
{
    public const string DefaultCurrency = "VND";

    public decimal Amount { get; }

    public string Currency { get; }

    private Money(decimal theAmount, string theCurrency)
    {
        Amount = theAmount;
        Currency = theCurrency;
    }

    public static Money Create(decimal theAmount, string theCurrency = DefaultCurrency)
    {
        if (theAmount < 0)
        {
            throw new DomainException("Money amount cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(theCurrency))
        {
            throw new DomainException("Currency is required.");
        }

        return new Money(theAmount, theCurrency.Trim().ToUpperInvariant());
    }

    public static Money Zero(string theCurrency = DefaultCurrency) => new(0, theCurrency);

    public Money Add(Money theOther)
    {
        EnsureSameCurrency(theOther);
        return new Money(Amount + theOther.Amount, Currency);
    }

    public Money Multiply(int theQuantity)
    {
        if (theQuantity < 0)
        {
            throw new DomainException("Quantity cannot be negative.");
        }

        return new Money(Amount * theQuantity, Currency);
    }

    private void EnsureSameCurrency(Money theOther)
    {
        if (Currency != theOther.Currency)
        {
            throw new DomainException($"Cannot operate on different currencies: {Currency} vs {theOther.Currency}.");
        }
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency;
    }

    public override string ToString() => $"{Amount} {Currency}";
}
