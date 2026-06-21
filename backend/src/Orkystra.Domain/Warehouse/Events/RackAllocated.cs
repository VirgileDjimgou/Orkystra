using Orkystra.Domain.Events;
using Orkystra.Domain.Identities;

namespace Orkystra.Domain.Warehouse.Events;

public sealed record RackAllocated(WarehouseId WarehouseId, ZoneId ZoneId, RackId RackId, string Code) : DomainEvent;
