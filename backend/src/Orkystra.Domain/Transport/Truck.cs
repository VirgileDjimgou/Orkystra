using Orkystra.Domain.Abstractions;
using Orkystra.Domain.Identities;
using Orkystra.Domain.ValueObjects;

namespace Orkystra.Domain.Transport;

public sealed class Truck : Entity<TruckId>
{
    public Truck(TruckId id, string reference, Weight capacity)
        : base(id)
    {
        Reference = reference;
        Capacity = capacity;
    }

    public string Reference { get; }

    public Weight Capacity { get; }

    public TruckStatus Status { get; private set; } = TruckStatus.Idle;

    public DriverId? AssignedDriverId { get; private set; }

    internal void AssignDriver(DriverId driverId)
    {
        AssignedDriverId = driverId;
        Status = TruckStatus.Assigned;
    }

    internal void Depart()
    {
        Status = TruckStatus.InTransit;
    }

    internal void Delay()
    {
        Status = TruckStatus.Delayed;
    }

    internal void Arrive()
    {
        Status = TruckStatus.Arrived;
    }
}
