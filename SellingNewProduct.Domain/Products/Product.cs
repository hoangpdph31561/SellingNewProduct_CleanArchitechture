using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.ValueObjects;

namespace SellingNewProduct.Domain.Products;

/// <summary>
/// A clothing product. Aggregate root. Belongs to a category (by id).
/// Guards its own stock and price invariants.
/// </summary>
public sealed class Product : AggregateRoot<Guid>
{
    public string Name { get; private set; } = default!;

    public Sku Sku { get; private set; } = default!;

    public string Color { get; private set; } = default!;

    public Size Size { get; private set; }

    public Money Price { get; private set; } = default!;

    public int StockQuantity { get; private set; }

    public Guid CategoryId { get; private set; }

    private Product()
    {
    }

    private Product(
        Guid theId,
        string theName,
        Sku theSku,
        string theColor,
        Size theSize,
        Money thePrice,
        int theStockQuantity,
        Guid theCategoryId)
        : base(theId)
    {
        Name = theName;
        Sku = theSku;
        Color = theColor;
        Size = theSize;
        Price = thePrice;
        StockQuantity = theStockQuantity;
        CategoryId = theCategoryId;
    }

    public static Product Create(
        string theName,
        Sku theSku,
        string theColor,
        Size theSize,
        Money thePrice,
        int theStockQuantity,
        Guid theCategoryId)
    {
        if (string.IsNullOrWhiteSpace(theName))
        {
            throw new DomainException("Product name is required.");
        }

        if (string.IsNullOrWhiteSpace(theColor))
        {
            throw new DomainException("Product color is required.");
        }

        if (thePrice.Amount <= 0)
        {
            throw new DomainException("Product price must be greater than zero.");
        }

        if (theStockQuantity < 0)
        {
            throw new DomainException("Stock quantity cannot be negative.");
        }

        if (theCategoryId == Guid.Empty)
        {
            throw new DomainException("Product must belong to a category.");
        }

        return new Product(Guid.NewGuid(), theName.Trim(), theSku, theColor.Trim(), theSize, thePrice, theStockQuantity, theCategoryId);
    }

    public void ChangePrice(Money theNewPrice)
    {
        if (theNewPrice.Amount <= 0)
        {
            throw new DomainException("Product price must be greater than zero.");
        }

        Price = theNewPrice;
        MarkUpdated();
    }

    public void IncreaseStock(int theQuantity)
    {
        if (theQuantity <= 0)
        {
            throw new DomainException("Increase quantity must be positive.");
        }

        StockQuantity += theQuantity;
        MarkUpdated();
    }

    public void DecreaseStock(int theQuantity)
    {
        if (theQuantity <= 0)
        {
            throw new DomainException("Decrease quantity must be positive.");
        }

        if (theQuantity > StockQuantity)
        {
            throw new DomainException($"Not enough stock for product '{Name}'. Available: {StockQuantity}, requested: {theQuantity}.");
        }

        StockQuantity -= theQuantity;
        MarkUpdated();
    }

    public static Product Rehydrate(
        Guid theId,
        string theName,
        Sku theSku,
        string theColor,
        Size theSize,
        Money thePrice,
        int theStockQuantity,
        Guid theCategoryId,
        EntityStatus theStatus,
        DateTime theCreatedAtUtc,
        DateTime? theUpdatedAtUtc)
    {
        var aProduct = new Product
        {
            Name = theName,
            Sku = theSku,
            Color = theColor,
            Size = theSize,
            Price = thePrice,
            StockQuantity = theStockQuantity,
            CategoryId = theCategoryId
        };

        aProduct.RestoreState(theId, theStatus, theCreatedAtUtc, theUpdatedAtUtc);
        return aProduct;
    }
}
