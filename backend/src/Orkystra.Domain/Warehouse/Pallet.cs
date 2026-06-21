using Orkystra.Domain.Abstractions;
using Orkystra.Domain.Identities;
using Orkystra.Domain.ValueObjects;

namespace Orkystra.Domain.Warehouse;

public sealed class Pallet : Entity<PalletId>
{
    public Pallet(PalletId id, string reference, Weight weight, Volume volume)
        : base(id)
    {
        Reference = reference;
        Weight = weight;
        Volume = volume;
    }

    public string Reference { get; }

    public Weight Weight { get; }

    public Volume Volume { get; }

    public PalletStatus Status { get; private set; } = PalletStatus.Received;

    public SlotId? CurrentSlotId { get; private set; }

    public void Store(SlotId slotId)
    {
        CurrentSlotId = slotId;
        Status = PalletStatus.Stored;
    }

    public void ClearSlot()
    {
        CurrentSlotId = null;
    }
}
