using Orkystra.Domain.Abstractions;
using Orkystra.Domain.Events;
using Orkystra.Domain.Identities;

namespace Orkystra.Domain.Tests;

public sealed class AggregateRootTests
{
    [Fact]
    public void AggregateRoot_TracksAndDequeuesDomainEvents()
    {
        var aggregate = TestAggregate.Create();

        aggregate.RecordEvent();

        Assert.Single(aggregate.DomainEvents);

        var dequeued = aggregate.DequeueDomainEvents();

        Assert.Single(dequeued);
        Assert.Empty(aggregate.DomainEvents);
    }

    [Fact]
    public void Entities_AreEqualWhenTypeAndIdentifierMatch()
    {
        var identifier = WarehouseId.New();
        var first = new TestAggregate(identifier);
        var second = new TestAggregate(identifier);

        Assert.Equal(first, second);
    }

    private sealed class TestAggregate : AggregateRoot<WarehouseId>
    {
        public TestAggregate(WarehouseId id)
            : base(id)
        {
        }

        public static TestAggregate Create() => new(WarehouseId.New());

        public void RecordEvent()
        {
            RaiseDomainEvent(new TestDomainEvent());
        }
    }

    private sealed record TestDomainEvent : DomainEvent;
}
