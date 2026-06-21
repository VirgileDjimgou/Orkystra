using Orkystra.Domain.Events;
using Orkystra.Domain.Identities;

namespace Orkystra.Domain.Transport.Events;

public sealed record TruckDelayed(RouteId RouteId, TruckId TruckId, string ReasonCode, int DelayMinutes) : DomainEvent;
