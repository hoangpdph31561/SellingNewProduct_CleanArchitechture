namespace SellingNewProduct.Domain.Common;

/// <summary>
/// Lifecycle status of every entity. Used for soft delete: records are never
/// physically removed, their status is set to <see cref="Deleted"/> instead.
/// </summary>
public enum EntityStatus
{
    Active = 1,
    Inactive = 2,
    Deleted = 3
}
