using Orkystra.Domain.Events;
using Orkystra.Domain.Identities;

namespace Orkystra.Domain.Transport.Events;

public sealed record RouteCreated(RouteId RouteId, TruckId TruckId, string Reference) : DomainEvent;
