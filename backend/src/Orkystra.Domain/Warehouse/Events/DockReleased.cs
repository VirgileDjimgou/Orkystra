using Orkystra.Domain.Events;
using Orkystra.Domain.Identities;

namespace Orkystra.Domain.Warehouse.Events;

public sealed record DockReleased(WarehouseId WarehouseId, DockId DockId) : DomainEvent;
