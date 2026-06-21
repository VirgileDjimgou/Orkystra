using Orkystra.Domain.Events;
using Orkystra.Domain.Identities;

namespace Orkystra.Domain.Warehouse.Events;

public sealed record ZoneCreated(WarehouseId WarehouseId, ZoneId ZoneId, string Code, string Name) : DomainEvent;
