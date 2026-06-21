using Orkystra.Domain.Abstractions;
using Orkystra.Domain.Identities;

namespace Orkystra.Domain.Warehouse;

public sealed class Rack : Entity<RackId>
{
    private readonly List<Slot> _slots = [];

    public Rack(RackId id, ZoneId zoneId, string code)
        : base(id)
    {
        ZoneId = zoneId;
        Code = code;
    }

    public ZoneId ZoneId { get; }

    public string Code { get; }

    public IReadOnlyCollection<Slot> Slots => _slots.AsReadOnly();

    internal void AddSlot(Slot slot)
    {
        _slots.Add(slot);
    }
}
