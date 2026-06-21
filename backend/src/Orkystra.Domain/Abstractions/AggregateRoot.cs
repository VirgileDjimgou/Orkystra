using Orkystra.Domain.Events;

namespace Orkystra.Domain.Abstractions;

public abstract class AggregateRoot<TId> : Entity<TId>
    where TId : struct, IEntityId
{
    private readonly List<DomainEvent> _domainEvents = [];

    protected AggregateRoot(TId id)
        : base(id)
    {
    }

    public IReadOnlyCollection<DomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void RaiseDomainEvent(DomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    public IReadOnlyCollection<DomainEvent> DequeueDomainEvents()
    {
        var domainEvents = _domainEvents.ToArray();
        _domainEvents.Clear();

        return domainEvents;
    }
}
