using Orkystra.Domain.Abstractions;
using Orkystra.Domain.Common;
using Orkystra.Domain.Identities;
using Orkystra.Domain.ValueObjects;
using Orkystra.Domain.Warehouse.Events;

namespace Orkystra.Domain.Warehouse;

public sealed class Warehouse : AggregateRoot<WarehouseId>
{
    private readonly List<Zone> _zones = [];
    private readonly List<Dock> _docks = [];
    private readonly List<Pallet> _pallets = [];

    private Warehouse(WarehouseId id, TenantId tenantId, string name)
        : base(id)
    {
        TenantId = tenantId;
        Name = name;
    }

    public TenantId TenantId { get; }

    public string Name { get; }

    public IReadOnlyCollection<Zone> Zones => _zones.AsReadOnly();

    public IReadOnlyCollection<Dock> Docks => _docks.AsReadOnly();

    public IReadOnlyCollection<Pallet> Pallets => _pallets.AsReadOnly();

    public static Result<Warehouse> Create(WarehouseId warehouseId, TenantId tenantId, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<Warehouse>(DomainErrors.Required(nameof(name)));
        }

        var warehouse = new Warehouse(warehouseId, tenantId, name.Trim());
        warehouse.RaiseDomainEvent(new WarehouseCreated(warehouse.Id, warehouse.TenantId, warehouse.Name));

        return Result.Success(warehouse);
    }

    public Result AddZone(ZoneId zoneId, string code, string name)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return Result.Failure(DomainErrors.Required(nameof(code)));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure(DomainErrors.Required(nameof(name)));
        }

        if (_zones.Any(zone => zone.Code.Equals(code, StringComparison.OrdinalIgnoreCase)))
        {
            return Result.Failure(DomainErrors.InvalidValue(nameof(code), "zone code must be unique within a warehouse"));
        }

        var zone = new Zone(zoneId, code.Trim(), name.Trim());
        _zones.Add(zone);
        RaiseDomainEvent(new ZoneCreated(Id, zone.Id, zone.Code, zone.Name));

        return Result.Success();
    }

    public Result AddRack(ZoneId zoneId, RackId rackId, string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return Result.Failure(DomainErrors.Required(nameof(code)));
        }

        var zone = _zones.SingleOrDefault(item => item.Id == zoneId);
        if (zone is null)
        {
            return Result.Failure(DomainErrors.InvalidValue(nameof(zoneId), "zone was not found"));
        }

        if (zone.Racks.Any(rack => rack.Code.Equals(code, StringComparison.OrdinalIgnoreCase)))
        {
            return Result.Failure(DomainErrors.InvalidValue(nameof(code), "rack code must be unique within a zone"));
        }

        var rack = new Rack(rackId, zoneId, code.Trim());
        zone.AddRack(rack);
        RaiseDomainEvent(new RackAllocated(Id, zoneId, rackId, rack.Code));

        return Result.Success();
    }

    public Result AddSlot(RackId rackId, SlotId slotId, string code, Weight maxWeight)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return Result.Failure(DomainErrors.Required(nameof(code)));
        }

        var rack = FindRack(rackId);
        if (rack is null)
        {
            return Result.Failure(DomainErrors.InvalidValue(nameof(rackId), "rack was not found"));
        }

        if (rack.Slots.Any(slot => slot.Code.Equals(code, StringComparison.OrdinalIgnoreCase)))
        {
            return Result.Failure(DomainErrors.InvalidValue(nameof(code), "slot code must be unique within a rack"));
        }

        rack.AddSlot(new Slot(slotId, rackId, code.Trim(), maxWeight));

        return Result.Success();
    }

    public Result AddDock(DockId dockId, string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return Result.Failure(DomainErrors.Required(nameof(code)));
        }

        if (_docks.Any(dock => dock.Code.Equals(code, StringComparison.OrdinalIgnoreCase)))
        {
            return Result.Failure(DomainErrors.InvalidValue(nameof(code), "dock code must be unique within a warehouse"));
        }

        _docks.Add(new Dock(dockId, code.Trim()));

        return Result.Success();
    }

    public Result ReceivePallet(PalletId palletId, string reference, Weight weight, Volume volume)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return Result.Failure(DomainErrors.Required(nameof(reference)));
        }

        if (_pallets.Any(pallet => pallet.Id == palletId))
        {
            return Result.Failure(DomainErrors.InvalidValue(nameof(palletId), "pallet already exists"));
        }

        var pallet = new Pallet(palletId, reference.Trim(), weight, volume);
        _pallets.Add(pallet);
        RaiseDomainEvent(new PalletReceived(Id, pallet.Id, pallet.Reference));

        return Result.Success();
    }

    public Result StorePallet(PalletId palletId, SlotId slotId)
    {
        var pallet = FindPallet(palletId);
        if (pallet is null)
        {
            return Result.Failure(DomainErrors.InvalidValue(nameof(palletId), "pallet was not found"));
        }

        var slot = FindSlot(slotId);
        if (slot is null)
        {
            return Result.Failure(DomainErrors.InvalidValue(nameof(slotId), "slot was not found"));
        }

        if (slot.IsOccupied)
        {
            return Result.Failure(DomainErrors.InvalidValue(nameof(slotId), "slot is already occupied"));
        }

        if (pallet.CurrentSlotId.HasValue)
        {
            return Result.Failure(DomainErrors.InvalidValue(nameof(palletId), "pallet is already stored and must be moved instead"));
        }

        if (pallet.Weight.Kilograms > slot.MaxWeight.Kilograms)
        {
            return Result.Failure(DomainErrors.InvalidValue(nameof(slotId), "slot max weight is exceeded"));
        }

        slot.Occupy(pallet.Id);
        pallet.Store(slot.Id);
        RaiseDomainEvent(new PalletStored(Id, pallet.Id, slot.Id));

        return Result.Success();
    }

    public Result MovePallet(PalletId palletId, SlotId targetSlotId)
    {
        var pallet = FindPallet(palletId);
        if (pallet is null)
        {
            return Result.Failure(DomainErrors.InvalidValue(nameof(palletId), "pallet was not found"));
        }

        if (!pallet.CurrentSlotId.HasValue)
        {
            return Result.Failure(DomainErrors.InvalidValue(nameof(palletId), "pallet is not currently stored"));
        }

        var currentSlot = FindSlot(pallet.CurrentSlotId.Value);
        var targetSlot = FindSlot(targetSlotId);

        if (currentSlot is null || targetSlot is null)
        {
            return Result.Failure(DomainErrors.InvalidValue(nameof(targetSlotId), "slot was not found"));
        }

        if (targetSlot.IsOccupied)
        {
            return Result.Failure(DomainErrors.InvalidValue(nameof(targetSlotId), "slot is already occupied"));
        }

        if (pallet.Weight.Kilograms > targetSlot.MaxWeight.Kilograms)
        {
            return Result.Failure(DomainErrors.InvalidValue(nameof(targetSlotId), "slot max weight is exceeded"));
        }

        currentSlot.Release();
        targetSlot.Occupy(pallet.Id);
        var sourceSlotId = pallet.CurrentSlotId.Value;
        pallet.Store(targetSlot.Id);
        RaiseDomainEvent(new PalletMoved(Id, pallet.Id, sourceSlotId, targetSlot.Id));

        return Result.Success();
    }

    public Result OccupyDock(DockId dockId, string operationReference)
    {
        if (string.IsNullOrWhiteSpace(operationReference))
        {
            return Result.Failure(DomainErrors.Required(nameof(operationReference)));
        }

        var dock = _docks.SingleOrDefault(item => item.Id == dockId);
        if (dock is null)
        {
            return Result.Failure(DomainErrors.InvalidValue(nameof(dockId), "dock was not found"));
        }

        if (dock.Status == DockStatus.Occupied)
        {
            return Result.Failure(DomainErrors.InvalidValue(nameof(dockId), "dock is already occupied"));
        }

        dock.Occupy(operationReference.Trim());
        RaiseDomainEvent(new DockOccupied(Id, dock.Id, dock.ActiveOperationReference!));

        return Result.Success();
    }

    public Result ReleaseDock(DockId dockId)
    {
        var dock = _docks.SingleOrDefault(item => item.Id == dockId);
        if (dock is null)
        {
            return Result.Failure(DomainErrors.InvalidValue(nameof(dockId), "dock was not found"));
        }

        if (dock.Status == DockStatus.Available)
        {
            return Result.Failure(DomainErrors.InvalidValue(nameof(dockId), "dock is already available"));
        }

        dock.Release();
        RaiseDomainEvent(new DockReleased(Id, dock.Id));

        return Result.Success();
    }

    public int OccupiedDockCount => _docks.Count(dock => dock.Status == DockStatus.Occupied);

    public int StoredPalletCount => _pallets.Count(pallet => pallet.Status == PalletStatus.Stored);

    private Rack? FindRack(RackId rackId) =>
        _zones.SelectMany(zone => zone.Racks).SingleOrDefault(rack => rack.Id == rackId);

    private Slot? FindSlot(SlotId slotId) =>
        _zones.SelectMany(zone => zone.Racks).SelectMany(rack => rack.Slots).SingleOrDefault(slot => slot.Id == slotId);

    private Pallet? FindPallet(PalletId palletId) =>
        _pallets.SingleOrDefault(pallet => pallet.Id == palletId);
}
