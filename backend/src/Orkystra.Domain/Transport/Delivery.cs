using Orkystra.Domain.Abstractions;
using Orkystra.Domain.Identities;

namespace Orkystra.Domain.Transport;

public sealed class Delivery : Entity<DeliveryId>
{
    public Delivery(DeliveryId id, ShipmentId shipmentId, StopId stopId)
        : base(id)
    {
        ShipmentId = shipmentId;
        StopId = stopId;
    }

    public ShipmentId ShipmentId { get; }

    public StopId StopId { get; }

    public bool IsCompleted { get; private set; }

    internal void Complete()
    {
        IsCompleted = true;
    }
}
