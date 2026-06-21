using Orkystra.Domain.Abstractions;
using Orkystra.Domain.Identities;
using Orkystra.Domain.ValueObjects;

namespace Orkystra.Domain.Warehouse;

public sealed class Slot : Entity<SlotId>
{
    public Slot(SlotId id, RackId rackId, string code, Weight maxWeight)
        : base(id)
    {
        RackId = rackId;
        Code = code;
        MaxWeight = maxWeight;
    }

    public RackId RackId { get; }

    public string Code { get; }

    public Weight MaxWeight { get; }

    public PalletId? OccupyingPalletId { get; private set; }

    public bool IsOccupied => OccupyingPalletId.HasValue;

    internal void Occupy(PalletId palletId)
    {
        OccupyingPalletId = palletId;
    }

    internal void Release()
    {
        OccupyingPalletId = null;
    }
}
