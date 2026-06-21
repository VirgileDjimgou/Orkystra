using Orkystra.Api.ControlTower;

namespace Orkystra.Domain.Tests;

public sealed class ControlTowerOverviewTests
{
    [Fact]
    public async Task ControlTowerOverviewService_builds_projection_payload_from_registered_sources()
    {
        var service = new ControlTowerOverviewService();

        var overview = await service.BuildOverviewAsync("north-hub-demo");

        Assert.Equal("north-hub-demo", overview.TenantId);
        Assert.NotEmpty(overview.Scenarios);
        Assert.NotEmpty(overview.Warehouses);
        Assert.NotEmpty(overview.Routes);
        Assert.NotEmpty(overview.Alerts);
        Assert.NotEmpty(overview.EventFeed);
        Assert.NotEmpty(overview.Providers);
        Assert.True(overview.GeneratedAtUtc <= DateTimeOffset.UtcNow);
        Assert.Equal(2, overview.Warehouses.Count);
        Assert.Equal(3, overview.Routes.Count);
        Assert.Contains(overview.Warehouses, warehouse => warehouse.Name == "West Flow Center");
        Assert.Contains(overview.Routes, route => route.Reference == "RT-318");
        Assert.Contains(overview.EventFeed, item => item.Title == "ProviderHealthObserved");
    }
}
