namespace SellingNewProduct.Domain.Common;

/// <summary>
/// Base class for value objects: no identity, immutable, compared by the
/// values of their components (see <see cref="GetEqualityComponents"/>).
/// </summary>
public abstract class ValueObject
{
    protected abstract IEnumerable<object?> GetEqualityComponents();

    public override bool Equals(object? theObject)
    {
        if (theObject is not ValueObject aOther || theObject.GetType() != GetType())
        {
            return false;
        }

        return GetEqualityComponents().SequenceEqual(aOther.GetEqualityComponents());
    }

    public override int GetHashCode()
    {
        var aHashCode = new HashCode();

        foreach (var aComponent in GetEqualityComponents())
        {
            aHashCode.Add(aComponent);
        }

        return aHashCode.ToHashCode();
    }
}
