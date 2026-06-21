using Orkystra.Domain.Abstractions;
using Orkystra.Domain.Identities;

namespace Orkystra.Domain.Transport;

public sealed class Driver : Entity<DriverId>
{
    public Driver(DriverId id, string name)
        : base(id)
    {
        Name = name;
    }

    public string Name { get; }
}
