using Orkystra.Domain.Identities;
using Orkystra.Domain.ValueObjects;
using Orkystra.Domain.Warehouse;
using Orkystra.Domain.Warehouse.Events;
using WarehouseAggregate = Orkystra.Domain.Warehouse.Warehouse;

namespace Orkystra.Domain.Tests;

public sealed class WarehouseTests
{
    [Fact]
    public void WarehouseCreate_RaisesWarehouseCreatedEvent()
    {
        var warehouseId = WarehouseId.New();
        var tenantId = TenantId.New();

        var result = WarehouseAggregate.Create(warehouseId, tenantId, "Main Warehouse");

        Assert.True(result.IsSuccess);
        Assert.Contains(result.Value.DomainEvents, domainEvent => domainEvent is WarehouseCreated created && created.WarehouseId == warehouseId);
    }

    [Fact]
    public void AddZone_AddRack_AddSlot_AndReceiveStorePallet_Succeeds()
    {
        var warehouse = CreateWarehouse();
        var zoneId = ZoneId.New();
        var rackId = RackId.New();
        var slotId = SlotId.New();
        var palletId = PalletId.New();

        var addZone = warehouse.AddZone(zoneId, "RECV", "Receiving");
        var addRack = warehouse.AddRack(zoneId, rackId, "R-01");
        var maxWeight = Weight.Create(150m).Value;
        var addSlot = warehouse.AddSlot(rackId, slotId, "S-01", maxWeight);
        var receivePallet = warehouse.ReceivePallet(palletId, "PAL-001", Weight.Create(50m).Value, Volume.Create(1.2m).Value);
        var storePallet = warehouse.StorePallet(palletId, slotId);

        Assert.True(addZone.IsSuccess);
        Assert.True(addRack.IsSuccess);
        Assert.True(addSlot.IsSuccess);
        Assert.True(receivePallet.IsSuccess);
        Assert.True(storePallet.IsSuccess);
        Assert.Equal(1, warehouse.StoredPalletCount);
        Assert.Contains(warehouse.DomainEvents, domainEvent => domainEvent is PalletStored stored && stored.PalletId == palletId);
    }

    [Fact]
    public void StorePallet_FailsWhenTargetSlotIsOccupied()
    {
        var warehouse = CreateWarehouseWithTwoPalletsOneSlot(out var firstPalletId, out var secondPalletId, out var slotId);

        warehouse.StorePallet(firstPalletId, slotId);

        var result = warehouse.StorePallet(secondPalletId, slotId);

        Assert.True(result.IsFailure);
        Assert.Contains("occupied", result.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MovePallet_FailsWhenTargetSlotIsOccupied()
    {
        var warehouse = CreateWarehouseWithTwoSlotsAndTwoPallets(
            out var firstPalletId,
            out var secondPalletId,
            out var sourceSlotId,
            out var occupiedTargetSlotId);

        warehouse.StorePallet(firstPalletId, sourceSlotId);
        warehouse.StorePallet(secondPalletId, occupiedTargetSlotId);

        var result = warehouse.MovePallet(firstPalletId, occupiedTargetSlotId);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void MovePallet_ReleasesSourceSlotAndOccupiesTargetSlot()
    {
        var warehouse = CreateWarehouseWithTwoSlotsAndTwoPallets(
            out var firstPalletId,
            out _,
            out var sourceSlotId,
            out var targetSlotId,
            storeSecondPallet: false);

        warehouse.StorePallet(firstPalletId, sourceSlotId);

        var result = warehouse.MovePallet(firstPalletId, targetSlotId);

        Assert.True(result.IsSuccess);
        Assert.Contains(warehouse.DomainEvents, domainEvent =>
            domainEvent is PalletMoved moved &&
            moved.FromSlotId == sourceSlotId &&
            moved.ToSlotId == targetSlotId);
    }

    [Fact]
    public void OccupyDock_FailsWhenDockIsAlreadyOccupied()
    {
        var warehouse = CreateWarehouse();
        var dockId = DockId.New();

        warehouse.AddDock(dockId, "D-01");
        warehouse.OccupyDock(dockId, "LOAD-1");

        var result = warehouse.OccupyDock(dockId, "LOAD-2");

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void ReleaseDock_FailsWhenDockIsAlreadyAvailable()
    {
        var warehouse = CreateWarehouse();
        var dockId = DockId.New();

        warehouse.AddDock(dockId, "D-01");

        var result = warehouse.ReleaseDock(dockId);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void OccupyAndReleaseDock_UpdatesWarehouseState()
    {
        var warehouse = CreateWarehouse();
        var dockId = DockId.New();

        warehouse.AddDock(dockId, "D-01");
        warehouse.OccupyDock(dockId, "LOAD-1");
        var releaseResult = warehouse.ReleaseDock(dockId);

        Assert.True(releaseResult.IsSuccess);
        Assert.Equal(0, warehouse.OccupiedDockCount);
        Assert.Contains(warehouse.DomainEvents, domainEvent => domainEvent is DockOccupied occupied && occupied.DockId == dockId);
        Assert.Contains(warehouse.DomainEvents, domainEvent => domainEvent is DockReleased released && released.DockId == dockId);
    }

    private static WarehouseAggregate CreateWarehouse()
    {
        return WarehouseAggregate.Create(WarehouseId.New(), TenantId.New(), "Main Warehouse").Value;
    }

    private static WarehouseAggregate CreateWarehouseWithTwoPalletsOneSlot(out PalletId firstPalletId, out PalletId secondPalletId, out SlotId slotId)
    {
        var warehouse = CreateWarehouseWithWarehouseTopology(out _, out _, out slotId);

        firstPalletId = PalletId.New();
        secondPalletId = PalletId.New();

        warehouse.ReceivePallet(firstPalletId, "PAL-001", Weight.Create(50m).Value, Volume.Create(1m).Value);
        warehouse.ReceivePallet(secondPalletId, "PAL-002", Weight.Create(40m).Value, Volume.Create(1m).Value);

        return warehouse;
    }

    private static WarehouseAggregate CreateWarehouseWithTwoSlotsAndTwoPallets(
        out PalletId firstPalletId,
        out PalletId secondPalletId,
        out SlotId sourceSlotId,
        out SlotId targetSlotId,
        bool storeSecondPallet = true)
    {
        var warehouse = CreateWarehouseWithWarehouseTopology(out _, out var rackId, out sourceSlotId);
        targetSlotId = SlotId.New();
        warehouse.AddSlot(rackId, targetSlotId, "S-02", Weight.Create(200m).Value);

        firstPalletId = PalletId.New();
        secondPalletId = PalletId.New();
        warehouse.ReceivePallet(firstPalletId, "PAL-001", Weight.Create(50m).Value, Volume.Create(1m).Value);
        warehouse.ReceivePallet(secondPalletId, "PAL-002", Weight.Create(40m).Value, Volume.Create(1m).Value);

        if (storeSecondPallet)
        {
            warehouse.StorePallet(secondPalletId, targetSlotId);
        }

        return warehouse;
    }

    private static WarehouseAggregate CreateWarehouseWithWarehouseTopology(out ZoneId zoneId, out RackId rackId, out SlotId slotId)
    {
        var warehouse = CreateWarehouse();
        zoneId = ZoneId.New();
        rackId = RackId.New();
        slotId = SlotId.New();

        warehouse.AddZone(zoneId, "RECV", "Receiving");
        warehouse.AddRack(zoneId, rackId, "R-01");
        warehouse.AddSlot(rackId, slotId, "S-01", Weight.Create(200m).Value);

        return warehouse;
    }
}
