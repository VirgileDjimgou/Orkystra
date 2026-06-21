using Orkystra.Api.ControlTower;

namespace Orkystra.Domain.Tests;

public sealed class WarehouseProjectionTests
{
    [Fact]
    public async Task WarehouseProjectionService_lists_summary_projections()
    {
        var service = new WarehouseProjectionService();

        var warehouses = await service.ListAsync();

        Assert.Equal(2, warehouses.Count);
        Assert.Contains(warehouses, warehouse => warehouse.Name == "North Hub A" && warehouse.ZoneCount == 4);
        Assert.Contains(warehouses, warehouse => warehouse.Name == "West Flow Center" && warehouse.StoredPalletCount == 401);
    }

    [Fact]
    public async Task WarehouseProjectionService_returns_detail_projection_for_known_warehouse()
    {
        var service = new WarehouseProjectionService();

        var warehouse = await service.GetByIdAsync(Guid.Parse("db9a789f-9df8-45ff-a252-96d4319c2f12"));

        Assert.NotNull(warehouse);
        Assert.Equal("North Hub A", warehouse!.Name);
        Assert.Equal(4, warehouse.Zones.Count);
        Assert.Contains(warehouse.Zones, zone => zone.Code == "XDK" && zone.Status == "Critical");
        Assert.Contains(warehouse.Docks, dock => dock.Code == "D-04" && dock.Status == "Available");
    }

    [Fact]
    public async Task WarehouseProjectionService_returns_null_for_unknown_warehouse()
    {
        var service = new WarehouseProjectionService();

        var warehouse = await service.GetByIdAsync(Guid.NewGuid());

        Assert.Null(warehouse);
    }
}
