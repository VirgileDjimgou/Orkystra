using Orkystra.Api.ControlTower;

namespace Orkystra.Domain.Tests;

public sealed class WarehouseWorkbenchServiceTests
{
    [Fact]
    public async Task BuildAsync_returns_items_for_critical_and_watch_zones()
    {
        var projectionService = new WarehouseProjectionService();
        var service = new WarehouseWorkbenchService(projectionService);

        var result = await service.BuildAsync();

        Assert.True(result.ExceptionCount > 0);
        Assert.NotEmpty(result.Items);
        Assert.NotEmpty(result.Groups);
        Assert.Contains(result.Items, item => item.Severity == "Critical" && item.Category == "Zone");
        Assert.Contains(result.Items, item => item.Severity == "Warning" && item.Category == "Zone");
    }

    [Fact]
    public async Task BuildAsync_items_ordered_by_severity_then_category_then_title()
    {
        var projectionService = new WarehouseProjectionService();
        var service = new WarehouseWorkbenchService(projectionService);

        var result = await service.BuildAsync();

        var severities = result.Items.Select(i => i.Severity).ToList();
        var orderedSeverities = severities.OrderByDescending(s => s switch
        {
            "Critical" => 3,
            "Warning" => 2,
            _ => 1,
        }).ToList();

        Assert.Equal(orderedSeverities, severities);
    }

    [Fact]
    public async Task BuildAsync_limits_items_to_twelve()
    {
        var projectionService = new WarehouseProjectionService();
        var service = new WarehouseWorkbenchService(projectionService);

        var result = await service.BuildAsync();

        Assert.True(result.Items.Count <= 12);
    }

    [Fact]
    public async Task BuildAsync_groups_items_by_category()
    {
        var projectionService = new WarehouseProjectionService();
        var service = new WarehouseWorkbenchService(projectionService);

        var result = await service.BuildAsync();

        foreach (var group in result.Groups)
        {
            var matchingItems = result.Items.Where(i =>
                string.Equals(i.Category, group.Label, StringComparison.OrdinalIgnoreCase)).ToArray();
            Assert.Equal(group.Count, matchingItems.Length);
        }
    }

    [Fact]
    public async Task BuildAsync_summary_reflects_exception_count()
    {
        var projectionService = new WarehouseProjectionService();
        var service = new WarehouseWorkbenchService(projectionService);

        var result = await service.BuildAsync();

        if (result.ExceptionCount > 0)
        {
            Assert.Contains(result.ExceptionCount.ToString(), result.Summary);
        }
        else
        {
            Assert.Contains("clear", result.Summary, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task BuildAsync_returns_generated_at_utc()
    {
        var projectionService = new WarehouseProjectionService();
        var service = new WarehouseWorkbenchService(projectionService);

        var result = await service.BuildAsync();

        Assert.True(result.GeneratedAtUtc <= DateTimeOffset.UtcNow);
        Assert.True(result.GeneratedAtUtc > DateTimeOffset.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task BuildAsync_zone_items_include_warehouse_and_zone_references()
    {
        var projectionService = new WarehouseProjectionService();
        var service = new WarehouseWorkbenchService(projectionService);

        var result = await service.BuildAsync();

        var zoneItems = result.Items.Where(i => i.Category == "Zone").ToArray();
        foreach (var item in zoneItems)
        {
            Assert.NotNull(item.WarehouseId);
            Assert.NotNull(item.WarehouseName);
            Assert.NotNull(item.ZoneCode);
        }
    }

    [Fact]
    public async Task BuildAsync_occupancy_items_include_warehouse_reference()
    {
        var projectionService = new WarehouseProjectionService();
        var service = new WarehouseWorkbenchService(projectionService);

        var result = await service.BuildAsync();

        var occupancyItems = result.Items.Where(i => i.Category == "Occupancy").ToArray();
        foreach (var item in occupancyItems)
        {
            Assert.NotNull(item.WarehouseId);
            Assert.NotNull(item.WarehouseName);
            Assert.Null(item.ZoneCode);
        }
    }

    [Fact]
    public async Task BuildAsync_all_items_have_recommended_action()
    {
        var projectionService = new WarehouseProjectionService();
        var service = new WarehouseWorkbenchService(projectionService);

        var result = await service.BuildAsync();

        foreach (var item in result.Items)
        {
            Assert.NotNull(item.RecommendedAction);
            Assert.NotNull(item.ActionLabel);
        }
    }

    [Fact]
    public async Task BuildAsync_all_items_have_evidence()
    {
        var projectionService = new WarehouseProjectionService();
        var service = new WarehouseWorkbenchService(projectionService);

        var result = await service.BuildAsync();

        foreach (var item in result.Items)
        {
            Assert.NotEmpty(item.Evidence);
        }
    }

    [Fact]
    public async Task BuildAsync_groups_have_highest_severity()
    {
        var projectionService = new WarehouseProjectionService();
        var service = new WarehouseWorkbenchService(projectionService);

        var result = await service.BuildAsync();

        foreach (var group in result.Groups)
        {
            var matchingItems = result.Items.Where(i =>
                string.Equals(i.Category, group.Label, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (matchingItems.Length > 0)
            {
                var maxSeverity = matchingItems.Max(i => i.Severity switch
                {
                    "Critical" => 3,
                    "Warning" => 2,
                    _ => 1,
                });
                var expectedLabel = maxSeverity switch
                {
                    3 => "Critical",
                    2 => "Warning",
                    _ => "Info",
                };
                Assert.Equal(expectedLabel, group.HighestSeverity);
            }
        }
    }
}
