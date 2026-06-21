using Orkystra.Domain.Events;
using Orkystra.Domain.Identities;

namespace Orkystra.Domain.Warehouse.Events;

public sealed record PalletMoved(WarehouseId WarehouseId, PalletId PalletId, SlotId FromSlotId, SlotId ToSlotId) : DomainEvent;
