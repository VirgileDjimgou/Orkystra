using Orkystra.Domain.Events;
using Orkystra.Domain.Identities;

namespace Orkystra.Domain.Warehouse.Events;

public sealed record PalletReceived(WarehouseId WarehouseId, PalletId PalletId, string Reference) : DomainEvent;
