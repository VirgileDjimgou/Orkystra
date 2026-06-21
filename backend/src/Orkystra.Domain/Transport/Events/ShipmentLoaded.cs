using Orkystra.Domain.Events;
using Orkystra.Domain.Identities;

namespace Orkystra.Domain.Transport.Events;

public sealed record ShipmentLoaded(RouteId RouteId, ShipmentId ShipmentId) : DomainEvent;
