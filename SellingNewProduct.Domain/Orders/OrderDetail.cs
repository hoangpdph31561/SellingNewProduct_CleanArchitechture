using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.ValueObjects;

namespace SellingNewProduct.Domain.Orders;

/// <summary>
/// A line in an order. Child entity of the <see cref="Order"/> aggregate:
/// it has no repository of its own and is only created/changed through Order.
/// Price and product name are snapshotted at the time of ordering.
/// </summary>
public sealed class OrderDetail : BaseEntity<Guid>
{
    public Guid ProductId { get; private set; }

    public string ProductName { get; private set; } = default!;

    public Money UnitPrice { get; private set; } = default!;

    public int Quantity { get; private set; }

    public Money LineTotal => UnitPrice.Multiply(Quantity);

    private OrderDetail()
    {
    }

    private OrderDetail(Guid theId, Guid theProductId, string theProductName, Money theUnitPrice, int theQuantity)
        : base(theId)
    {
        ProductId = theProductId;
        ProductName = theProductName;
        UnitPrice = theUnitPrice;
        Quantity = theQuantity;
    }

    /// <summary>Created only by the Order aggregate.</summary>
    internal static OrderDetail Create(Guid theProductId, string theProductName, Money theUnitPrice, int theQuantity)
    {
        if (theQuantity <= 0)
        {
            throw new DomainException("Order detail quantity must be positive.");
        }

        return new OrderDetail(Guid.NewGuid(), theProductId, theProductName, theUnitPrice, theQuantity);
    }

    internal void IncreaseQuantity(int theQuantity)
    {
        if (theQuantity <= 0)
        {
            throw new DomainException("Quantity to add must be positive.");
        }

        Quantity += theQuantity;
        MarkUpdated();
    }

    internal void ChangeQuantity(int theNewQuantity)
    {
        if (theNewQuantity <= 0)
        {
            throw new DomainException("Order detail quantity must be positive.");
        }

        Quantity = theNewQuantity;
        MarkUpdated();
    }

    public static OrderDetail Rehydrate(
        Guid theId,
        Guid theProductId,
        string theProductName,
        Money theUnitPrice,
        int theQuantity,
        EntityStatus theStatus,
        DateTime theCreatedAtUtc,
        DateTime? theUpdatedAtUtc)
    {
        var aDetail = new OrderDetail
        {
            ProductId = theProductId,
            ProductName = theProductName,
            UnitPrice = theUnitPrice,
            Quantity = theQuantity
        };

        aDetail.RestoreState(theId, theStatus, theCreatedAtUtc, theUpdatedAtUtc);
        return aDetail;
    }
}
