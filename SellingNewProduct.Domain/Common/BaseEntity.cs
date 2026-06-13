namespace SellingNewProduct.Domain.Common;

/// <summary>
/// Base class for every entity. Carries identity, audit timestamps and a
/// lifecycle <see cref="EntityStatus"/> used for soft delete.
/// Equality is based on <see cref="Id"/>.
/// </summary>
public abstract class BaseEntity<TId> where TId : notnull
{
    public TId Id { get; protected set; } = default!;

    public DateTime CreatedAtUtc { get; protected set; }

    public DateTime? UpdatedAtUtc { get; protected set; }

    public EntityStatus Status { get; protected set; }

    protected BaseEntity(TId theId)
    {
        Id = theId;
        CreatedAtUtc = DateTime.UtcNow;
        Status = EntityStatus.Active;
    }

    /// <summary>Required by persistence layers to rehydrate the entity.</summary>
    protected BaseEntity()
    {
    }

    public void Activate()
    {
        Status = EntityStatus.Active;
        MarkUpdated();
    }

    public void Deactivate()
    {
        Status = EntityStatus.Inactive;
        MarkUpdated();
    }

    /// <summary>Soft delete: flag the entity as deleted, never remove it physically.</summary>
    public void Delete()
    {
        Status = EntityStatus.Deleted;
        MarkUpdated();
    }

    protected void MarkUpdated()
    {
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Restore audit/identity fields when rehydrating from persistence.
    /// Called by aggregate <c>Rehydrate</c> factories, never by business code.
    /// </summary>
    protected void RestoreState(TId theId, EntityStatus theStatus, DateTime theCreatedAtUtc, DateTime? theUpdatedAtUtc)
    {
        Id = theId;
        Status = theStatus;
        CreatedAtUtc = theCreatedAtUtc;
        UpdatedAtUtc = theUpdatedAtUtc;
    }

    public override bool Equals(object? theObject)
    {
        if (theObject is not BaseEntity<TId> aOther)
        {
            return false;
        }

        if (ReferenceEquals(this, aOther))
        {
            return true;
        }

        return Id.Equals(aOther.Id);
    }

    public override int GetHashCode() => Id.GetHashCode();
}
