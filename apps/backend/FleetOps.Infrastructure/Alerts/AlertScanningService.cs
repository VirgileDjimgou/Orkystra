using System.Globalization;
using FleetOps.Core.Modules.Alerts;
using FleetOps.Core.Modules.Integrations;
using FleetOps.Core.Modules.Maintenance;
using FleetOps.Infrastructure.Identity;
using FleetOps.Infrastructure.Integrations;
using FleetOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FleetOps.Infrastructure.Alerts;

public sealed partial class AlertScanningService(
    FleetOpsDbContext dbContext,
    TimeProvider timeProvider,
    IOptions<AlertingOptions> options,
    IIntegrationOutboxService integrationOutboxService,
    IDevAlertNotifier devAlertNotifier,
    ILogger<AlertScanningService> logger) : IAlertScanningService
{
    public async Task<AlertScanResult> ScanAllOrganizationsAsync(CancellationToken cancellationToken)
    {
        var organizationIds = await dbContext.Organizations
            .AsNoTracking()
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var aggregate = new AlertScanAccumulator();
        foreach (var organizationId in organizationIds)
        {
            var result = await ScanOrganizationAsync(organizationId, cancellationToken);
            aggregate.Add(result);
        }

        return aggregate.ToResult();
    }

    public async Task<AlertScanResult> ScanOrganizationAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var settings = options.Value;
        var findings = new List<AlertFinding>();

        findings.AddRange(await BuildDocumentFindingsAsync(
            organizationId,
            now,
            settings.DocumentDueSoonDays,
            cancellationToken));
        findings.AddRange(await BuildMaintenanceFindingsAsync(
            organizationId,
            now,
            settings.MaintenanceDueSoonDays,
            settings.MaintenanceDueSoonKilometers,
            cancellationToken));
        findings.AddRange(await BuildInactiveVehicleFindingsAsync(
            organizationId,
            now,
            settings.InactiveVehicleAfterHours,
            cancellationToken));

        var dedupeKeys = findings.Select(x => x.DeduplicationKey).ToHashSet(StringComparer.Ordinal);
        var existingAlerts = await dbContext.OperationalAlerts
            .Where(x => x.OrganizationId == organizationId)
            .ToListAsync(cancellationToken);

        var nowUtc = now.ToUniversalTime();
        var result = new AlertScanAccumulator();
        foreach (var finding in findings)
        {
            var existing = existingAlerts.FirstOrDefault(x => x.DeduplicationKey == finding.DeduplicationKey);
            if (existing is null)
            {
                var created = new OperationalAlert(
                    organizationId,
                    finding.RuleType,
                    finding.DeduplicationKey,
                    finding.Severity,
                    finding.Title,
                    finding.Message,
                    finding.TargetType,
                    finding.TargetEntityId,
                    nowUtc);
                dbContext.OperationalAlerts.Add(created);
                existingAlerts.Add(created);

                if (finding.RuleType is AlertRuleType.VehicleMaintenanceByDate or AlertRuleType.VehicleMaintenanceByMileage)
                {
                    var sourceKey = $"alert:{created.Id:D}";
                    dbContext.MaintenanceWorkOrders.Add(new MaintenanceWorkOrder(
                        organizationId,
                        finding.TargetEntityId,
                        finding.Title,
                        sourceKey,
                        finding.Severity == AlertSeverity.Critical ? 3 : 2,
                        nowUtc,
                        finding.Severity == AlertSeverity.Critical));
                }

                dbContext.AlertNotifications.Add(new AlertNotification(
                    organizationId,
                    created.Id,
                    AlertNotificationChannel.InApp,
                    created.Title,
                    created.Message,
                    nowUtc));

                result.CreatedAlerts++;
                result.InAppNotifications++;

                try
                {
                    await devAlertNotifier.SendAsync(created, cancellationToken);
                    dbContext.AlertNotifications.Add(new AlertNotification(
                        organizationId,
                        created.Id,
                        AlertNotificationChannel.EmailDev,
                        created.Title,
                        created.Message,
                        nowUtc));
                    result.EmailNotifications++;
                }
                catch (Exception ex)
                {
                    Log.DevAlertEmailFailed(
                        logger,
                        ex,
                        organizationId,
                        finding.DeduplicationKey);
                    result.EmailFailures++;
                }

                await integrationOutboxService.PublishAsync(
                    organizationId,
                    IntegrationEventType.AlertOpened,
                    "alert",
                    created.Id.ToString(),
                    new
                    {
                        alertId = created.Id,
                        ruleType = created.RuleType.ToString(),
                        severity = created.Severity.ToString(),
                        created.Title,
                        created.Message,
                        created.TargetType,
                        created.TargetEntityId
                    },
                    cancellationToken);

                continue;
            }

            var shouldRefresh =
                existing.ResolvedAtUtc is not null
                || existing.Severity != finding.Severity
                || !string.Equals(existing.Title, finding.Title, StringComparison.Ordinal)
                || !string.Equals(existing.Message, finding.Message, StringComparison.Ordinal)
                || existing.LastDetectedAtUtc != nowUtc;

            if (shouldRefresh)
            {
                existing.Refresh(finding.Severity, finding.Title, finding.Message, nowUtc);
                result.RefreshedAlerts++;
            }
        }

        foreach (var alert in existingAlerts.Where(x => x.IsOpen && !dedupeKeys.Contains(x.DeduplicationKey)))
        {
            alert.Resolve(nowUtc);
            result.ResolvedAlerts++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return result.ToResult();
    }

    private async Task<List<AlertFinding>> BuildDocumentFindingsAsync(
        Guid organizationId,
        DateTimeOffset now,
        int dueSoonDays,
        CancellationToken cancellationToken)
    {
        var documents = await (
            from document in dbContext.ComplianceDocuments.AsNoTracking()
            join vehicle in dbContext.Vehicles.AsNoTracking()
                on document.TargetEntityId equals vehicle.Id into vehicleJoin
            from vehicle in vehicleJoin.DefaultIfEmpty()
            join driver in dbContext.Drivers.AsNoTracking()
                on document.TargetEntityId equals driver.Id into driverJoin
            from driver in driverJoin.DefaultIfEmpty()
            where document.OrganizationId == organizationId
            select new
            {
                Document = document,
                VehicleRegistration = vehicle != null ? vehicle.RegistrationNumber : null,
                DriverName = driver != null ? driver.FullName : null
            })
            .ToListAsync(cancellationToken);

        var dueSoonThreshold = now.AddDays(dueSoonDays);
        var findings = new List<AlertFinding>();
        foreach (var item in documents)
        {
            if (item.Document.ExpiresAtUtc > dueSoonThreshold)
            {
                continue;
            }

            var expired = item.Document.ExpiresAtUtc <= now;
            var subjectName = item.Document.TargetType == ComplianceDocumentTargetType.Vehicle
                ? item.VehicleRegistration ?? "Unknown vehicle"
                : item.DriverName ?? "Unknown driver";
            var titlePrefix = expired ? "Expired" : "Expiring soon";
            var severity = expired ? AlertSeverity.Critical : AlertSeverity.Warning;
            var ruleType = item.Document.TargetType == ComplianceDocumentTargetType.Vehicle
                ? AlertRuleType.VehicleDocumentExpiry
                : AlertRuleType.DriverDocumentExpiry;
            findings.Add(new AlertFinding(
                DeduplicationKey: $"document:{item.Document.TargetType}:{item.Document.TargetEntityId}:{item.Document.DocumentType}".ToLowerInvariant(),
                RuleType: ruleType,
                Severity: severity,
                Title: $"{titlePrefix} {item.Document.DocumentType}",
                Message: $"{subjectName} document {item.Document.DocumentType} expires on {item.Document.ExpiresAtUtc:yyyy-MM-dd}.",
                TargetType: item.Document.TargetType == ComplianceDocumentTargetType.Vehicle ? "vehicle" : "driver",
                TargetEntityId: item.Document.TargetEntityId));
        }

        return findings;
    }

    private async Task<List<AlertFinding>> BuildMaintenanceFindingsAsync(
        Guid organizationId,
        DateTimeOffset now,
        int dueSoonDays,
        int dueSoonKilometers,
        CancellationToken cancellationToken)
    {
        var plans = await (
            from plan in dbContext.VehicleMaintenancePlans.AsNoTracking()
            join vehicle in dbContext.Vehicles.AsNoTracking()
                on plan.VehicleId equals vehicle.Id
            where plan.OrganizationId == organizationId && plan.IsActive
            select new
            {
                Plan = plan,
                VehicleRegistration = vehicle.RegistrationNumber,
                CurrentOdometerKm = vehicle.CurrentOdometerKm
            })
            .ToListAsync(cancellationToken);

        var findings = new List<AlertFinding>();
        foreach (var item in plans)
        {
            if (item.Plan.IntervalDays is int intervalDays)
            {
                var dueAt = item.Plan.LastCompletedAtUtc.AddDays(intervalDays);
                if (dueAt <= now || dueAt <= now.AddDays(dueSoonDays))
                {
                    var overdue = dueAt <= now;
                    findings.Add(new AlertFinding(
                        DeduplicationKey: $"maintenance-date:{item.Plan.Id}".ToLowerInvariant(),
                        RuleType: AlertRuleType.VehicleMaintenanceByDate,
                        Severity: overdue ? AlertSeverity.Critical : AlertSeverity.Warning,
                        Title: overdue ? "Overdue maintenance by date" : "Maintenance due soon by date",
                        Message: $"{item.VehicleRegistration} plan {item.Plan.Title} is due on {dueAt:yyyy-MM-dd}.",
                        TargetType: "vehicle",
                        TargetEntityId: item.Plan.VehicleId));
                }
            }

            if (item.Plan.IntervalKilometers is int intervalKilometers)
            {
                var dueAtKm = item.Plan.LastCompletedOdometerKm + intervalKilometers;
                if (item.CurrentOdometerKm >= dueAtKm || item.CurrentOdometerKm >= dueAtKm - dueSoonKilometers)
                {
                    var overdue = item.CurrentOdometerKm >= dueAtKm;
                    findings.Add(new AlertFinding(
                        DeduplicationKey: $"maintenance-km:{item.Plan.Id}".ToLowerInvariant(),
                        RuleType: AlertRuleType.VehicleMaintenanceByMileage,
                        Severity: overdue ? AlertSeverity.Critical : AlertSeverity.Warning,
                        Title: overdue ? "Overdue maintenance by mileage" : "Maintenance due soon by mileage",
                        Message: $"{item.VehicleRegistration} plan {item.Plan.Title} is due at {dueAtKm} km and currently reads {item.CurrentOdometerKm} km.",
                        TargetType: "vehicle",
                        TargetEntityId: item.Plan.VehicleId));
                }
            }
        }

        return findings;
    }

    private async Task<List<AlertFinding>> BuildInactiveVehicleFindingsAsync(
        Guid organizationId,
        DateTimeOffset now,
        int inactiveVehicleAfterHours,
        CancellationToken cancellationToken)
    {
        var cutoff = now.AddHours(-inactiveVehicleAfterHours);
        var currentPositions = await dbContext.CurrentVehiclePositions
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId)
            .ToDictionaryAsync(x => x.VehicleId, cancellationToken);
        var activeVehicles = await dbContext.Vehicles
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.IsActive)
            .OrderBy(x => x.RegistrationNumber)
            .ToListAsync(cancellationToken);

        var findings = new List<AlertFinding>();
        foreach (var vehicle in activeVehicles)
        {
            if (!currentPositions.TryGetValue(vehicle.Id, out var position) || position.RecordedAtUtc <= cutoff)
            {
                var lastSeen = position?.RecordedAtUtc.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture) ?? "never";
                findings.Add(new AlertFinding(
                    DeduplicationKey: $"inactive-vehicle:{vehicle.Id}".ToLowerInvariant(),
                    RuleType: AlertRuleType.VehicleInactive,
                    Severity: AlertSeverity.Warning,
                    Title: "Inactive vehicle",
                    Message: $"{vehicle.RegistrationNumber} has no fresh telemetry. Last seen: {lastSeen}.",
                    TargetType: "vehicle",
                    TargetEntityId: vehicle.Id));
            }
        }

        return findings;
    }

    private sealed class AlertScanAccumulator
    {
        public int CreatedAlerts { get; set; }
        public int RefreshedAlerts { get; set; }
        public int ResolvedAlerts { get; set; }
        public int InAppNotifications { get; set; }
        public int EmailNotifications { get; set; }
        public int EmailFailures { get; set; }

        public void Add(AlertScanResult result)
        {
            CreatedAlerts += result.CreatedAlerts;
            RefreshedAlerts += result.RefreshedAlerts;
            ResolvedAlerts += result.ResolvedAlerts;
            InAppNotifications += result.InAppNotifications;
            EmailNotifications += result.EmailNotifications;
            EmailFailures += result.EmailFailures;
        }

        public AlertScanResult ToResult() => new(
            CreatedAlerts,
            RefreshedAlerts,
            ResolvedAlerts,
            InAppNotifications,
            EmailNotifications,
            EmailFailures);
    }

    private sealed record AlertFinding(
        string DeduplicationKey,
        AlertRuleType RuleType,
        AlertSeverity Severity,
        string Title,
        string Message,
        string TargetType,
        Guid TargetEntityId);

    private static partial class Log
    {
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Warning,
            Message = "DEV alert email failed for organization {OrganizationId} and key {DeduplicationKey}.")]
        public static partial void DevAlertEmailFailed(
            ILogger logger,
            Exception exception,
            Guid organizationId,
            string deduplicationKey);
    }
}
