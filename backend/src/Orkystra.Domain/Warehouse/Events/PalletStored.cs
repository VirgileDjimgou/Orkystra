using Orkystra.Domain.Events;
using Orkystra.Domain.Identities;

namespace Orkystra.Domain.Warehouse.Events;

public sealed record PalletStored(WarehouseId WarehouseId, PalletId PalletId, SlotId SlotId) : DomainEvent;
