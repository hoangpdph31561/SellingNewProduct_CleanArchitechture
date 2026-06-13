namespace SellingNewProduct.Domain.Common;

/// <summary>
/// Root of an aggregate: the only entry point for changing the cluster of
/// entities it owns, and the only place that raises domain events.
/// </summary>
public abstract class AggregateRoot<TId> : BaseEntity<TId> where TId : notnull
{
    private readonly List<IDomainEvent> myListDomainEvents = new();

    public IReadOnlyList<IDomainEvent> DomainEvents => myListDomainEvents.AsReadOnly();

    protected AggregateRoot(TId theId) : base(theId)
    {
    }

    protected AggregateRoot()
    {
    }

    protected void Raise(IDomainEvent theDomainEvent)
    {
        myListDomainEvents.Add(theDomainEvent);
    }

    public void ClearDomainEvents()
    {
        myListDomainEvents.Clear();
    }
}
