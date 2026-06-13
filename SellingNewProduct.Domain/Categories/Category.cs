using SellingNewProduct.Domain.Common;

namespace SellingNewProduct.Domain.Categories;

/// <summary>
/// A product category (e.g. Shirts, Trousers, Dresses). Aggregate root.
/// </summary>
public sealed class Category : AggregateRoot<Guid>
{
    public string Name { get; private set; } = default!;

    public string Description { get; private set; } = default!;

    private Category()
    {
    }

    private Category(Guid theId, string theName, string theDescription)
        : base(theId)
    {
        Name = theName;
        Description = theDescription;
    }

    public static Category Create(string theName, string theDescription)
    {
        if (string.IsNullOrWhiteSpace(theName))
        {
            throw new DomainException("Category name is required.");
        }

        return new Category(Guid.NewGuid(), theName.Trim(), theDescription?.Trim() ?? string.Empty);
    }

    public void Update(string theName, string theDescription)
    {
        if (string.IsNullOrWhiteSpace(theName))
        {
            throw new DomainException("Category name is required.");
        }

        Name = theName.Trim();
        Description = theDescription?.Trim() ?? string.Empty;
        MarkUpdated();
    }

    public static Category Rehydrate(
        Guid theId,
        string theName,
        string theDescription,
        EntityStatus theStatus,
        DateTime theCreatedAtUtc,
        DateTime? theUpdatedAtUtc)
    {
        var aCategory = new Category
        {
            Name = theName,
            Description = theDescription
        };

        aCategory.RestoreState(theId, theStatus, theCreatedAtUtc, theUpdatedAtUtc);
        return aCategory;
    }
}
