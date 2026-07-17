using FleetOps.Core.Modules.Maintenance;
using Xunit;

namespace FleetOps.UnitTests;

public sealed class MaintenanceWorkOrderTests
{
    [Fact]
    public void CompletionRestoresVehicleAvailabilityAndKeepsCost()
    {
        var order = new MaintenanceWorkOrder(Guid.NewGuid(), Guid.NewGuid(), "Brake repair", "inspection:critical:1", 3, DateTimeOffset.UtcNow, true);
        order.SetCost(120.125m, 30m, "eur", "Workshop", "Pads", null);
        order.Complete("Repair verified", DateTimeOffset.UtcNow);

        Assert.False(order.IsVehicleUnavailable);
        Assert.Equal(150.13m, order.TotalCost);
        Assert.Equal("EUR", order.CurrencyCode);
        Assert.Equal(MaintenanceWorkOrderStatus.Completed, order.Status);
    }

    [Fact]
    public void ScheduleRequiresReasonAndValidWindow()
    {
        var order = new MaintenanceWorkOrder(Guid.NewGuid(), Guid.NewGuid(), "Oil", "alert:1", 1, DateTimeOffset.UtcNow, false);
        Assert.Throws<ArgumentException>(() => order.Schedule(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), ""));
        Assert.Throws<ArgumentException>(() => order.Schedule(DateTimeOffset.UtcNow.AddHours(1), DateTimeOffset.UtcNow, "plan"));
    }
}
