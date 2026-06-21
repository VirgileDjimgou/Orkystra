using Orkystra.Domain.Abstractions;
using Orkystra.Domain.Identities;

namespace Orkystra.Domain.Warehouse;

public sealed class Zone : Entity<ZoneId>
{
    private readonly List<Rack> _racks = [];

    public Zone(ZoneId id, string code, string name)
        : base(id)
    {
        Code = code;
        Name = name;
    }

    public string Code { get; }

    public string Name { get; }

    public IReadOnlyCollection<Rack> Racks => _racks.AsReadOnly();

    internal void AddRack(Rack rack)
    {
        _racks.Add(rack);
    }
}
