using FleetOps.Core.Modules.Alerts;
using FleetOps.Core.Modules.Fleet;
using FleetOps.Core.Modules.Identity;
using FleetOps.Core.Modules.Tracking;
using FleetOps.Infrastructure.Alerts;
using FleetOps.Infrastructure.Integrations;
using FleetOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace FleetOps.UnitTests;

public sealed class AlertScanningServiceTests
{
    [Fact]
    public async Task ScanOrganizationAsyncDeduplicatesDocumentAlertsAcrossRuns()
    {
        await using var dbContext = CreateDbContext();
        var organization = new Organization("Northwind Logistics", "northwind");
        var vehicle = new Vehicle(organization.Id, "NW-100", "Dispatch van");
        dbContext.Organizations.Add(organization);
        dbContext.Vehicles.Add(vehicle);
        dbContext.CurrentVehiclePositions.Add(CreateCurrentPosition(organization.Id, vehicle.Id));
        dbContext.ComplianceDocuments.Add(new ComplianceDocument(
            organization.Id,
            ComplianceDocumentTargetType.Vehicle,
            vehicle.Id,
            "Insurance",
            "POL-001",
            new DateTimeOffset(2026, 7, 20, 9, 0, 0, TimeSpan.Zero)));
        await dbContext.SaveChangesAsync();

        var options = Options.Create(new AlertingOptions { DocumentDueSoonDays = 30 });
        var timeProvider = new StaticTimeProvider(new DateTimeOffset(2026, 7, 16, 8, 0, 0, TimeSpan.Zero));

        var firstService = new AlertScanningService(
            dbContext,
            timeProvider,
            options,
            new FakeIntegrationOutboxService(),
            new FakeDevAlertNotifier(),
            NullLogger<AlertScanningService>.Instance);
        var secondService = new AlertScanningService(
            dbContext,
            timeProvider,
            options,
            new FakeIntegrationOutboxService(),
            new FakeDevAlertNotifier(),
            NullLogger<AlertScanningService>.Instance);

        var first = await firstService.ScanOrganizationAsync(organization.Id, CancellationToken.None);
        var second = await secondService.ScanOrganizationAsync(organization.Id, CancellationToken.None);

        Assert.Equal(1, first.CreatedAlerts);
        Assert.Equal(0, second.CreatedAlerts);
        var documentAlertIds = dbContext.OperationalAlerts
            .Where(x => x.OrganizationId == organization.Id && x.RuleType == AlertRuleType.VehicleDocumentExpiry)
            .Select(x => x.Id)
            .ToHashSet();
        Assert.Single(documentAlertIds);
        Assert.Equal(2, dbContext.AlertNotifications.Count(x => x.OrganizationId == organization.Id && documentAlertIds.Contains(x.AlertId)));
    }

    [Fact]
    public async Task ScanOrganizationAsyncUsesUtcDatesForOffsetExpiry()
    {
        await using var dbContext = CreateDbContext();
        var organization = new Organization("Northwind Logistics", "northwind");
        var driver = new Driver(organization.Id, "Alex North", "NW-DL-001");
        var vehicle = new Vehicle(organization.Id, "NW-105", "Compliance mule");
        dbContext.Organizations.Add(organization);
        dbContext.Drivers.Add(driver);
        dbContext.Vehicles.Add(vehicle);
        dbContext.CurrentVehiclePositions.Add(CreateCurrentPosition(organization.Id, vehicle.Id));
        dbContext.ComplianceDocuments.Add(new ComplianceDocument(
            organization.Id,
            ComplianceDocumentTargetType.Driver,
            driver.Id,
            "License",
            "DL-100",
            new DateTimeOffset(2026, 7, 17, 1, 30, 0, TimeSpan.FromHours(2))));
        await dbContext.SaveChangesAsync();

        var service = new AlertScanningService(
            dbContext,
            new StaticTimeProvider(new DateTimeOffset(2026, 7, 16, 0, 0, 0, TimeSpan.Zero)),
            Options.Create(new AlertingOptions { DocumentDueSoonDays = 5 }),
            new FakeIntegrationOutboxService(),
            new FakeDevAlertNotifier(),
            NullLogger<AlertScanningService>.Instance);

        await service.ScanOrganizationAsync(organization.Id, CancellationToken.None);

        var alert = await dbContext.OperationalAlerts.SingleAsync();
        Assert.Contains("2026-07-16", alert.Message);
    }

    [Fact]
    public async Task ScanOrganizationAsyncContinuesWhenDevEmailFails()
    {
        await using var dbContext = CreateDbContext();
        var organization = new Organization("Northwind Logistics", "northwind");
        var vehicle = new Vehicle(organization.Id, "NW-200", "Reserve van");
        dbContext.Organizations.Add(organization);
        dbContext.Vehicles.Add(vehicle);
        dbContext.CurrentVehiclePositions.Add(CreateCurrentPosition(organization.Id, vehicle.Id));
        dbContext.ComplianceDocuments.Add(new ComplianceDocument(
            organization.Id,
            ComplianceDocumentTargetType.Vehicle,
            vehicle.Id,
            "Inspection",
            "INSP-001",
            new DateTimeOffset(2026, 7, 18, 9, 0, 0, TimeSpan.Zero)));
        await dbContext.SaveChangesAsync();

        var service = new AlertScanningService(
            dbContext,
            new StaticTimeProvider(new DateTimeOffset(2026, 7, 16, 0, 0, 0, TimeSpan.Zero)),
            Options.Create(new AlertingOptions()),
            new FakeIntegrationOutboxService(),
            new ThrowingDevAlertNotifier(),
            NullLogger<AlertScanningService>.Instance);

        var result = await service.ScanOrganizationAsync(organization.Id, CancellationToken.None);

        Assert.Equal(1, result.CreatedAlerts);
        Assert.Equal(1, result.EmailFailures);
        var alertIds = dbContext.OperationalAlerts
            .Where(x => x.RuleType == AlertRuleType.VehicleDocumentExpiry)
            .Select(x => x.Id)
            .ToHashSet();
        Assert.Single(dbContext.AlertNotifications.Where(x => x.Channel == AlertNotificationChannel.InApp && alertIds.Contains(x.AlertId)));
        Assert.Empty(dbContext.AlertNotifications.Where(x => x.Channel == AlertNotificationChannel.EmailDev));
    }

    [Fact]
    public async Task ScanOrganizationAsyncResolvesMaintenanceAlertAfterCompletion()
    {
        await using var dbContext = CreateDbContext();
        var organization = new Organization("Northwind Logistics", "northwind");
        var vehicle = new Vehicle(organization.Id, "NW-300", "Service van");
        vehicle.UpdateCurrentOdometer(10_500);
        var plan = new VehicleMaintenancePlan(
            organization.Id,
            vehicle.Id,
            "Oil change",
            intervalKilometers: 5_000,
            intervalDays: null,
            lastCompletedOdometerKm: 5_000,
            lastCompletedAtUtc: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        dbContext.Organizations.Add(organization);
        dbContext.Vehicles.Add(vehicle);
        dbContext.CurrentVehiclePositions.Add(CreateCurrentPosition(organization.Id, vehicle.Id));
        dbContext.VehicleMaintenancePlans.Add(plan);
        await dbContext.SaveChangesAsync();

        var options = Options.Create(new AlertingOptions { MaintenanceDueSoonKilometers = 200 });
        var timeProvider = new StaticTimeProvider(new DateTimeOffset(2026, 7, 16, 0, 0, 0, TimeSpan.Zero));

        var firstService = new AlertScanningService(
            dbContext,
            timeProvider,
            options,
            new FakeIntegrationOutboxService(),
            new FakeDevAlertNotifier(),
            NullLogger<AlertScanningService>.Instance);
        var secondService = new AlertScanningService(
            dbContext,
            timeProvider,
            options,
            new FakeIntegrationOutboxService(),
            new FakeDevAlertNotifier(),
            NullLogger<AlertScanningService>.Instance);

        var first = await firstService.ScanOrganizationAsync(organization.Id, CancellationToken.None);
        var alert = await dbContext.OperationalAlerts.SingleAsync(x => x.RuleType == AlertRuleType.VehicleMaintenanceByMileage);
        Assert.Null(alert.ResolvedAtUtc);
        Assert.Equal(1, first.CreatedAlerts);

        plan.MarkCompleted(10_500, new DateTimeOffset(2026, 7, 16, 2, 0, 0, TimeSpan.Zero));
        await dbContext.SaveChangesAsync();

        var second = await secondService.ScanOrganizationAsync(organization.Id, CancellationToken.None);
        alert = await dbContext.OperationalAlerts.SingleAsync(x => x.RuleType == AlertRuleType.VehicleMaintenanceByMileage);

        Assert.Equal(1, second.ResolvedAlerts);
        Assert.NotNull(alert.ResolvedAtUtc);
    }

    private static FleetOpsDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<FleetOpsDbContext>()
            .UseInMemoryDatabase($"alerts-tests-{Guid.NewGuid():N}")
            .Options;
        var dbContext = new FleetOpsDbContext(options);
        dbContext.Database.EnsureCreated();
        return dbContext;
    }

    private sealed class StaticTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeDevAlertNotifier : IDevAlertNotifier
    {
        public Task SendAsync(Core.Modules.Alerts.OperationalAlert alert, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class ThrowingDevAlertNotifier : IDevAlertNotifier
    {
        public Task SendAsync(Core.Modules.Alerts.OperationalAlert alert, CancellationToken cancellationToken)
            => throw new InvalidOperationException("SMTP unavailable");
    }

    private sealed class FakeIntegrationOutboxService : IIntegrationOutboxService
    {
        public Task<int> PublishAsync(
            Guid organizationId,
            string eventType,
            string aggregateType,
            string aggregateId,
            object payload,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(0);
        }
    }

    private static CurrentVehiclePosition CreateCurrentPosition(Guid organizationId, Guid vehicleId)
    {
        return new CurrentVehiclePosition(
            organizationId,
            vehicleId,
            "TEST-DEVICE",
            $"event-{vehicleId:N}",
            new DateTimeOffset(2026, 7, 16, 8, 0, 0, TimeSpan.Zero),
            48.8566,
            2.3522,
            0,
            90);
    }
}
