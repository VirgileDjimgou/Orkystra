using Orkystra.Domain.Events;
using Orkystra.Domain.Identities;

namespace Orkystra.Domain.Transport.Events;

public sealed record DeliveryCompleted(RouteId RouteId, DeliveryId DeliveryId, ShipmentId ShipmentId) : DomainEvent;
