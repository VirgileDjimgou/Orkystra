using Orkystra.Domain.Events;
using Orkystra.Domain.Identities;

namespace Orkystra.Domain.Transport.Events;

public sealed record TruckDeparted(RouteId RouteId, TruckId TruckId) : DomainEvent;
