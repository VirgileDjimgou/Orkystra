using Orkystra.Domain.Events;
using Orkystra.Domain.Identities;

namespace Orkystra.Domain.Warehouse.Events;

public sealed record DockOccupied(WarehouseId WarehouseId, DockId DockId, string OperationReference) : DomainEvent;
