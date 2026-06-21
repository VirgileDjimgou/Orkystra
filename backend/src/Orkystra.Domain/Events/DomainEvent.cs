namespace Orkystra.Domain.Events;

public abstract record DomainEvent
{
    protected DomainEvent()
    {
        EventId = Guid.NewGuid();
        OccurredAt = DateTimeOffset.UtcNow;
    }

    public Guid EventId { get; init; }

    public DateTimeOffset OccurredAt { get; init; }

    public virtual int SchemaVersion => 1;

    public string EventType => GetType().Name;
}
