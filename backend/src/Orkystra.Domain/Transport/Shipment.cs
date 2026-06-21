using Orkystra.Domain.Abstractions;
using Orkystra.Domain.Identities;
using Orkystra.Domain.ValueObjects;

namespace Orkystra.Domain.Transport;

public sealed class Shipment : Entity<ShipmentId>
{
    public Shipment(ShipmentId id, string reference, Weight loadWeight, OrderId? orderId = null)
        : base(id)
    {
        Reference = reference;
        LoadWeight = loadWeight;
        OrderId = orderId;
    }

    public string Reference { get; }

    public Weight LoadWeight { get; }

    public OrderId? OrderId { get; }

    public ShipmentStatus Status { get; private set; } = ShipmentStatus.Created;

    internal void AssignToRoute()
    {
        Status = ShipmentStatus.Assigned;
    }

    internal void Load()
    {
        Status = ShipmentStatus.Loaded;
    }

    internal void Depart()
    {
        Status = ShipmentStatus.Departed;
    }

    internal void Arrive()
    {
        Status = ShipmentStatus.Arrived;
    }

    internal void Complete()
    {
        Status = ShipmentStatus.Completed;
    }
}
