namespace SellingNewProduct.Domain.Common;

/// <summary>
/// Marker for something that has already happened in the domain
/// (e.g. an order was confirmed). Raised by aggregate roots.
/// </summary>
public interface IDomainEvent
{
    DateTime OccurredOnUtc { get; }
}
