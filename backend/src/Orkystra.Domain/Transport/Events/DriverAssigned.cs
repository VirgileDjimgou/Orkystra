using Orkystra.Domain.Events;
using Orkystra.Domain.Identities;

namespace Orkystra.Domain.Transport.Events;

public sealed record DriverAssigned(RouteId RouteId, TruckId TruckId, DriverId DriverId) : DomainEvent;
