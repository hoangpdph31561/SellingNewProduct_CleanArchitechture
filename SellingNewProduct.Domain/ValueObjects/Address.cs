using SellingNewProduct.Domain.Common;

namespace SellingNewProduct.Domain.ValueObjects;

/// <summary>
/// A postal address. Immutable; replaced as a whole rather than mutated.
/// </summary>
public sealed class Address : ValueObject
{
    public string Street { get; }

    public string Ward { get; }

    public string District { get; }

    public string City { get; }

    public string Country { get; }

    private Address(string theStreet, string theWard, string theDistrict, string theCity, string theCountry)
    {
        Street = theStreet;
        Ward = theWard;
        District = theDistrict;
        City = theCity;
        Country = theCountry;
    }

    public static Address Create(
        string theStreet,
        string theWard,
        string theDistrict,
        string theCity,
        string theCountry = "Vietnam")
    {
        if (string.IsNullOrWhiteSpace(theStreet))
        {
            throw new DomainException("Street is required.");
        }

        if (string.IsNullOrWhiteSpace(theCity))
        {
            throw new DomainException("City is required.");
        }

        return new Address(
            theStreet.Trim(),
            theWard?.Trim() ?? string.Empty,
            theDistrict?.Trim() ?? string.Empty,
            theCity.Trim(),
            string.IsNullOrWhiteSpace(theCountry) ? "Vietnam" : theCountry.Trim());
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Street;
        yield return Ward;
        yield return District;
        yield return City;
        yield return Country;
    }

    public override string ToString() => $"{Street}, {Ward}, {District}, {City}, {Country}";
}
