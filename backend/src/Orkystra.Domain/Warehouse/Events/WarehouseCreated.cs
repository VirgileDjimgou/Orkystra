using Orkystra.Domain.Events;
using Orkystra.Domain.Identities;

namespace Orkystra.Domain.Warehouse.Events;

public sealed record WarehouseCreated(WarehouseId WarehouseId, TenantId TenantId, string Name) : DomainEvent;
