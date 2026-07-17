using FleetOps.Api.Auditing;
using FleetOps.Api.Operations;
using FleetOps.Api.Security;
using FleetOps.Core.Modules.Alerts;
using FleetOps.Core.Modules.Identity;
using FleetOps.Infrastructure.Alerts;
using FleetOps.Infrastructure.Identity;
using FleetOps.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FleetOps.Api.Alerts;

public static class AlertEndpointExtensions
{
    private const string ReaderRoles = SystemRoles.Admin + "," + SystemRoles.Operator;

    public static IEndpointRouteBuilder MapAlertEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/alerts")
            .RequireAuthorization(new AuthorizeAttribute { Roles = ReaderRoles });

        group.MapGet("/", ListAlertsAsync);
        group.MapGet("/dashboard", GetDashboardAsync);
        group.MapGet("/notifications", ListNotificationsAsync);
        group.MapGet("/assignees", ListAssigneesAsync);
        group.MapPost("/scan", ScanAlertsAsync);
        group.MapPost("/{id:guid}/assign", AssignAlertAsync);
        group.MapPost("/{id:guid}/acknowledge", AcknowledgeAlertAsync);

        return app;
    }

    private static async Task<IResult> ListAlertsAsync(
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        var alerts = await LoadAlertItemsAsync(dbContext, tenant.OrganizationId, cancellationToken);
        return Results.Ok(alerts);
    }

    private static async Task<IResult> GetDashboardAsync(
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        var alerts = await LoadAlertItemsAsync(dbContext, tenant.OrganizationId, cancellationToken);
        var notifications = await dbContext.AlertNotifications
            .AsNoTracking()
            .Where(x => x.OrganizationId == tenant.OrganizationId)
            .OrderByDescending(x => x.SentAtUtc)
            .Take(12)
            .Select(x => new AlertNotificationResponse(
                x.Id,
                x.AlertId,
                x.Channel,
                x.Subject,
                x.Body,
                x.SentAtUtc))
            .ToListAsync(cancellationToken);

        var summary = new AlertSummaryResponse(
            OpenCount: alerts.Count(x => x.ResolvedAtUtc is null),
            AcknowledgedCount: alerts.Count(x => x.Status == AlertStatus.Acknowledged && x.ResolvedAtUtc is null),
            CriticalCount: alerts.Count(x => x.Severity == AlertSeverity.Critical && x.ResolvedAtUtc is null),
            WarningCount: alerts.Count(x => x.Severity == AlertSeverity.Warning && x.ResolvedAtUtc is null),
            InactiveVehicleCount: alerts.Count(x => x.RuleType == AlertRuleType.VehicleInactive && x.ResolvedAtUtc is null),
            MaintenanceCount: alerts.Count(x =>
                (x.RuleType == AlertRuleType.VehicleMaintenanceByDate || x.RuleType == AlertRuleType.VehicleMaintenanceByMileage)
                && x.ResolvedAtUtc is null),
            ComplianceCount: alerts.Count(x =>
                (x.RuleType == AlertRuleType.VehicleDocumentExpiry || x.RuleType == AlertRuleType.DriverDocumentExpiry)
                && x.ResolvedAtUtc is null),
            TopAlerts: alerts.Take(8).ToList(),
            RecentNotifications: notifications);

        return Results.Ok(summary);
    }

    private static async Task<IResult> ListNotificationsAsync(
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        var notifications = await dbContext.AlertNotifications
            .AsNoTracking()
            .Where(x => x.OrganizationId == tenant.OrganizationId)
            .OrderByDescending(x => x.SentAtUtc)
            .Select(x => new AlertNotificationResponse(
                x.Id,
                x.AlertId,
                x.Channel,
                x.Subject,
                x.Body,
                x.SentAtUtc))
            .ToListAsync(cancellationToken);

        return Results.Ok(notifications);
    }

    private static async Task<IResult> ListAssigneesAsync(
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        ICurrentTenantAccessor currentTenantAccessor,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        var users = await userManager.Users
            .AsNoTracking()
            .Where(x => x.OrganizationId == tenant.OrganizationId && x.IsActive)
            .OrderBy(x => x.FullName)
            .ToListAsync(cancellationToken);

        var results = new List<AlertAssigneeResponse>();
        foreach (var user in users)
        {
            var roles = await userManager.GetRolesAsync(user);
            var role = roles.FirstOrDefault(x => x is SystemRoles.Admin or SystemRoles.Operator);
            if (role is null)
            {
                continue;
            }

            results.Add(new AlertAssigneeResponse(user.Id, user.FullName, user.Email ?? string.Empty, role));
        }

        return Results.Ok(results);
    }

    private static async Task<IResult> ScanAlertsAsync(
        HttpContext httpContext,
        IAlertScanningService alertScanningService,
        ICurrentTenantAccessor currentTenantAccessor,
        IAuditService auditService,
        IOperationsRealtimeNotifier notifier,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        var result = await alertScanningService.ScanOrganizationAsync(tenant.OrganizationId, cancellationToken);

        await auditService.WriteAsync(
            tenant.OrganizationId,
            tenant.UserId,
            "alerts.scan_requested",
            "alerts",
            null,
            new
            {
                result.CreatedAlerts,
                result.RefreshedAlerts,
                result.ResolvedAlerts,
                result.EmailFailures
            },
            cancellationToken);
        await notifier.NotifyQueueChangedAsync(tenant.OrganizationId, "alerts.scan_requested", cancellationToken);

        return Results.Ok(new ScanAlertsResponse(
            result.CreatedAlerts,
            result.RefreshedAlerts,
            result.ResolvedAlerts,
            result.InAppNotifications,
            result.EmailNotifications,
            result.EmailFailures));
    }

    private static async Task<IResult> AssignAlertAsync(
        Guid id,
        AssignAlertRequest request,
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        ICurrentTenantAccessor currentTenantAccessor,
        IAuditService auditService,
        IOperationsRealtimeNotifier notifier,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        var alert = await dbContext.OperationalAlerts
            .FirstOrDefaultAsync(x => x.OrganizationId == tenant.OrganizationId && x.Id == id, cancellationToken);
        if (alert is null)
        {
            return Results.NotFound();
        }

        if (alert.RowVersion != request.RowVersion)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["rowVersion"] = ["Alert was modified by another request. Reload and try again."]
            }, statusCode: StatusCodes.Status409Conflict);
        }

        var assignee = await userManager.Users
            .FirstOrDefaultAsync(
                x => x.OrganizationId == tenant.OrganizationId && x.Id == request.AssignedToUserId && x.IsActive,
                cancellationToken);
        if (assignee is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["assignedToUserId"] = ["Assignee must be an active user in the same organization."]
            });
        }

        var roles = await userManager.GetRolesAsync(assignee);
        if (!roles.Any(x => x is SystemRoles.Admin or SystemRoles.Operator))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["assignedToUserId"] = ["Assignee must have Admin or Operator role."]
            });
        }

        try
        {
            alert.Assign(assignee.Id, assignee.FullName, timeProvider.GetUtcNow());
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["assignedToUserId"] = [ex.Message]
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(
            tenant.OrganizationId,
            tenant.UserId,
            "alerts.assigned",
            "alert",
            alert.Id.ToString(),
            new { assignee.Id, assignee.FullName },
            cancellationToken);
        await notifier.NotifyQueueChangedAsync(tenant.OrganizationId, "alerts.assigned", cancellationToken);

        var response = await BuildAlertItemAsync(dbContext, alert, cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> AcknowledgeAlertAsync(
        Guid id,
        AcknowledgeAlertRequest request,
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        ICurrentTenantAccessor currentTenantAccessor,
        IAuditService auditService,
        IOperationsRealtimeNotifier notifier,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        var alert = await dbContext.OperationalAlerts
            .FirstOrDefaultAsync(x => x.OrganizationId == tenant.OrganizationId && x.Id == id, cancellationToken);
        if (alert is null)
        {
            return Results.NotFound();
        }

        if (alert.RowVersion != request.RowVersion)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["rowVersion"] = ["Alert was modified by another request. Reload and try again."]
            }, statusCode: StatusCodes.Status409Conflict);
        }

        var user = await userManager.Users
            .FirstAsync(x => x.Id == tenant.UserId, cancellationToken);

        try
        {
            alert.Acknowledge(user.Id, user.FullName, timeProvider.GetUtcNow());
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["state"] = [ex.Message]
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(
            tenant.OrganizationId,
            tenant.UserId,
            "alerts.acknowledged",
            "alert",
            alert.Id.ToString(),
            null,
            cancellationToken);
        await notifier.NotifyQueueChangedAsync(tenant.OrganizationId, "alerts.acknowledged", cancellationToken);

        var response = await BuildAlertItemAsync(dbContext, alert, cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<List<AlertListItemResponse>> LoadAlertItemsAsync(
        FleetOpsDbContext dbContext,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var alerts = await dbContext.OperationalAlerts
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId)
            .OrderByDescending(x => x.ResolvedAtUtc == null)
            .ThenByDescending(x => x.Severity)
            .ThenByDescending(x => x.LastDetectedAtUtc)
            .ToListAsync(cancellationToken);

        var vehicleLabels = await dbContext.Vehicles
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId)
            .ToDictionaryAsync(x => x.Id, x => x.RegistrationNumber, cancellationToken);
        var driverLabels = await dbContext.Drivers
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId)
            .ToDictionaryAsync(x => x.Id, x => x.FullName, cancellationToken);

        return alerts.Select(x => MapAlertItem(x, vehicleLabels, driverLabels)).ToList();
    }

    private static async Task<AlertListItemResponse> BuildAlertItemAsync(
        FleetOpsDbContext dbContext,
        OperationalAlert alert,
        CancellationToken cancellationToken)
    {
        var vehicleLabels = await dbContext.Vehicles
            .AsNoTracking()
            .Where(x => x.OrganizationId == alert.OrganizationId)
            .ToDictionaryAsync(x => x.Id, x => x.RegistrationNumber, cancellationToken);
        var driverLabels = await dbContext.Drivers
            .AsNoTracking()
            .Where(x => x.OrganizationId == alert.OrganizationId)
            .ToDictionaryAsync(x => x.Id, x => x.FullName, cancellationToken);
        return MapAlertItem(alert, vehicleLabels, driverLabels);
    }

    private static AlertListItemResponse MapAlertItem(
        OperationalAlert alert,
        IReadOnlyDictionary<Guid, string> vehicleLabels,
        IReadOnlyDictionary<Guid, string> driverLabels)
    {
        var targetLabel = alert.TargetType.Equals("vehicle", StringComparison.OrdinalIgnoreCase)
            ? vehicleLabels.GetValueOrDefault(alert.TargetEntityId, "Unknown vehicle")
            : driverLabels.GetValueOrDefault(alert.TargetEntityId, "Unknown driver");

        return new AlertListItemResponse(
            alert.Id,
            alert.RuleType,
            alert.Severity,
            alert.Status,
            alert.Title,
            alert.Message,
            alert.TargetType,
            alert.TargetEntityId,
            targetLabel,
            alert.AssignedToUserId,
            alert.AssignedToDisplayName,
            alert.AcknowledgedByUserId,
            alert.AcknowledgedByDisplayName,
            alert.LastDetectedAtUtc,
            alert.AssignedAtUtc,
            alert.AcknowledgedAtUtc,
            alert.ResolvedAtUtc,
            alert.RowVersion);
    }
}
