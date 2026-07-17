using System.Text.Json;
using FleetOps.Api.Auditing;
using FleetOps.Api.Security;
using FleetOps.Core.Modules.Alerts;
using FleetOps.Core.Modules.Dispatch;
using FleetOps.Core.Modules.Identity;
using FleetOps.Core.Modules.Operations;
using FleetOps.Infrastructure.Identity;
using FleetOps.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FleetOps.Api.Operations;

public static class OperationsCenterEndpointExtensions
{
    private const string ReaderRoles = SystemRoles.Admin + "," + SystemRoles.Operator;

    public static IEndpointRouteBuilder MapOperationsCenterEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/operations")
            .RequireAuthorization(new AuthorizeAttribute { Roles = ReaderRoles });

        group.MapGet("/exceptions", ListExceptionsAsync);
        group.MapGet("/saved-views", ListSavedViewsAsync);
        group.MapPost("/saved-views", CreateSavedViewAsync);
        group.MapPut("/saved-views/{id:guid}", UpdateSavedViewAsync);
        group.MapPost("/exceptions/{id}/assign", AssignAsync);
        group.MapPost("/exceptions/{id}/acknowledge", AcknowledgeAsync);
        group.MapPost("/exceptions/{id}/resolve", ResolveAsync);
        group.MapPost("/exceptions/{id}/snooze", SnoozeAsync);
        group.MapPost("/exceptions/bulk", BulkAsync);

        return app;
    }

    private static async Task<IResult> ListExceptionsAsync(
        string? search,
        string? sourceType,
        string? severity,
        string? workflowStatus,
        Guid? assignedToUserId,
        bool? includeSnoozed,
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        var showSnoozed = includeSnoozed ?? false;
        var items = await BuildQueueAsync(dbContext, tenant.OrganizationId, cancellationToken);

        var filtered = items
            .Where(item => showSnoozed || item.SnoozedUntilUtc is null || item.SnoozedUntilUtc <= DateTimeOffset.UtcNow)
            .Where(item => string.IsNullOrWhiteSpace(search) || item.SearchText.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase))
            .Where(item => string.IsNullOrWhiteSpace(sourceType) || item.SourceType.Equals(sourceType, StringComparison.OrdinalIgnoreCase))
            .Where(item => string.IsNullOrWhiteSpace(severity) || item.Severity.Equals(severity, StringComparison.OrdinalIgnoreCase))
            .Where(item => string.IsNullOrWhiteSpace(workflowStatus) || item.WorkflowStatus.Equals(workflowStatus, StringComparison.OrdinalIgnoreCase))
            .Where(item => assignedToUserId is null || item.AssignedToUserId == assignedToUserId)
            .ToList();

        var summary = new OperationsQueueSummaryResponse(
            filtered.Count,
            filtered.Count(x => x.Severity == "Critical"),
            filtered.Count(x => x.Severity == "Warning"),
            filtered.Count(x => x.SnoozedUntilUtc is not null && x.SnoozedUntilUtc > DateTimeOffset.UtcNow),
            filtered.Count(x => x.AssignedToUserId is null));

        return Results.Ok(new OperationsExceptionQueueResponse(summary, filtered));
    }

    private static async Task<IResult> ListSavedViewsAsync(
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        var views = await dbContext.OperationsSavedViews
            .AsNoTracking()
            .Where(x => x.OrganizationId == tenant.OrganizationId && (x.IsShared || x.CreatedByUserId == tenant.UserId))
            .OrderByDescending(x => x.IsShared)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return Results.Ok(views.Select(MapSavedView).ToList());
    }

    private static async Task<IResult> CreateSavedViewAsync(
        CreateOperationsSavedViewRequest request,
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        var entity = new OperationsSavedView(
            tenant.OrganizationId,
            tenant.UserId,
            request.Name,
            JsonSerializer.Serialize(request.Filters),
            request.IsShared);

        dbContext.OperationsSavedViews.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditService.WriteAsync(
            tenant.OrganizationId,
            tenant.UserId,
            "operations.saved_view_created",
            "operations_saved_view",
            entity.Id.ToString(),
            new { entity.Name, entity.IsShared },
            cancellationToken);

        return Results.Created($"/api/v1/operations/saved-views/{entity.Id}", MapSavedView(entity));
    }

    private static async Task<IResult> UpdateSavedViewAsync(
        Guid id,
        UpdateOperationsSavedViewRequest request,
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        var view = await dbContext.OperationsSavedViews
            .FirstOrDefaultAsync(x => x.OrganizationId == tenant.OrganizationId && x.Id == id, cancellationToken);
        if (view is null)
        {
            return Results.NotFound();
        }

        if (!view.IsShared && view.CreatedByUserId != tenant.UserId)
        {
            return Results.Forbid();
        }

        if (view.RowVersion != request.RowVersion)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["rowVersion"] = ["Saved view was modified by another request. Reload and try again."]
            }, statusCode: StatusCodes.Status409Conflict);
        }

        view.Update(request.Name, JsonSerializer.Serialize(request.Filters), request.IsShared);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditService.WriteAsync(
            tenant.OrganizationId,
            tenant.UserId,
            "operations.saved_view_updated",
            "operations_saved_view",
            view.Id.ToString(),
            new { view.Name, view.IsShared },
            cancellationToken);

        return Results.Ok(MapSavedView(view));
    }

    private static Task<IResult> AssignAsync(
        string id,
        OperationsAssignRequest request,
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        UserManager<ApplicationUser> userManager,
        IAuditService auditService,
        IOperationsRealtimeNotifier notifier,
        CancellationToken cancellationToken) =>
        ExecuteActionAsync(
            id,
            request.ConcurrencyToken,
            httpContext,
            dbContext,
            currentTenantAccessor,
            async (item, tenant, state, now) =>
            {
                var assignee = await userManager.Users.FirstOrDefaultAsync(
                    x => x.OrganizationId == tenant.OrganizationId && x.Id == request.AssignedToUserId && x.IsActive,
                    cancellationToken);
                if (assignee is null)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["assignedToUserId"] = ["Assignee must exist in the same organization."]
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

                state.Assign(assignee.Id, assignee.FullName, now);
                return null;
            },
            "operations.exception_assigned",
            new { request.AssignedToUserId },
            auditService,
            notifier,
            cancellationToken);

    private static Task<IResult> AcknowledgeAsync(
        string id,
        OperationsActionRequest request,
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        IAuditService auditService,
        IOperationsRealtimeNotifier notifier,
        CancellationToken cancellationToken) =>
        ExecuteActionAsync(
            id,
            request.ConcurrencyToken,
            httpContext,
            dbContext,
            currentTenantAccessor,
            (item, tenant, state, now) =>
            {
                state.Acknowledge(tenant.UserId, tenant.Email, now);
                return Task.FromResult<IResult?>(null);
            },
            "operations.exception_acknowledged",
            null,
            auditService,
            notifier,
            cancellationToken);

    private static Task<IResult> ResolveAsync(
        string id,
        OperationsResolveRequest request,
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        IAuditService auditService,
        IOperationsRealtimeNotifier notifier,
        CancellationToken cancellationToken) =>
        ExecuteActionAsync(
            id,
            request.ConcurrencyToken,
            httpContext,
            dbContext,
            currentTenantAccessor,
            async (item, tenant, state, now) =>
            {
                state.Resolve(tenant.UserId, tenant.Email, request.Reason, now);
                await AppendTimelineAsync(item, tenant.OrganizationId, dbContext, $"Exception resolved: {request.Reason}", now, cancellationToken);
                return null;
            },
            "operations.exception_resolved",
            new { request.Reason },
            auditService,
            notifier,
            cancellationToken);

    private static Task<IResult> SnoozeAsync(
        string id,
        OperationsSnoozeRequest request,
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        IAuditService auditService,
        IOperationsRealtimeNotifier notifier,
        CancellationToken cancellationToken) =>
        ExecuteActionAsync(
            id,
            request.ConcurrencyToken,
            httpContext,
            dbContext,
            currentTenantAccessor,
            async (item, tenant, state, now) =>
            {
                state.Snooze(request.SnoozedUntilUtc, request.Reason);
                await AppendTimelineAsync(item, tenant.OrganizationId, dbContext, $"Exception snoozed until {request.SnoozedUntilUtc:O}: {request.Reason}", now, cancellationToken);
                return null;
            },
            "operations.exception_snoozed",
            new { request.SnoozedUntilUtc, request.Reason },
            auditService,
            notifier,
            cancellationToken);

    private static async Task<IResult> BulkAsync(
        OperationsBulkActionRequest request,
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        UserManager<ApplicationUser> userManager,
        IAuditService auditService,
        IOperationsRealtimeNotifier notifier,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        var failures = new List<string>();
        var successCount = 0;

        foreach (var item in request.Items)
        {
            IResult result = request.Action switch
            {
                "assign" when request.AssignedToUserId.HasValue => await AssignAsync(
                    item.Id,
                    new OperationsAssignRequest(request.AssignedToUserId.Value, item.ConcurrencyToken),
                    httpContext,
                    dbContext,
                    currentTenantAccessor,
                    userManager,
                    auditService,
                    notifier,
                    cancellationToken),
                "acknowledge" => await AcknowledgeAsync(
                    item.Id,
                    new OperationsActionRequest(item.ConcurrencyToken),
                    httpContext,
                    dbContext,
                    currentTenantAccessor,
                    auditService,
                    notifier,
                    cancellationToken),
                "resolve" when !string.IsNullOrWhiteSpace(request.Reason) => await ResolveAsync(
                    item.Id,
                    new OperationsResolveRequest(item.ConcurrencyToken, request.Reason),
                    httpContext,
                    dbContext,
                    currentTenantAccessor,
                    auditService,
                    notifier,
                    cancellationToken),
                "snooze" when request.SnoozedUntilUtc.HasValue && !string.IsNullOrWhiteSpace(request.Reason) => await SnoozeAsync(
                    item.Id,
                    new OperationsSnoozeRequest(item.ConcurrencyToken, request.SnoozedUntilUtc.Value, request.Reason),
                    httpContext,
                    dbContext,
                    currentTenantAccessor,
                    auditService,
                    notifier,
                    cancellationToken),
                _ => Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["action"] = ["Unsupported bulk action payload."]
                }),
            };

            if (result is IStatusCodeHttpResult status && status.StatusCode is >= 200 and < 300)
            {
                successCount++;
            }
            else
            {
                failures.Add(item.Id);
            }
        }

        return Results.Ok(new OperationsBulkActionResponse(successCount, failures));
    }

    private static async Task<IResult> ExecuteActionAsync(
        string id,
        string concurrencyToken,
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        Func<OperationsExceptionListItemResponse, CurrentTenant, OperationsExceptionState, DateTimeOffset, Task<IResult?>> mutateAsync,
        string auditAction,
        object? auditMetadata,
        IAuditService auditService,
        IOperationsRealtimeNotifier notifier,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        var item = (await BuildQueueAsync(dbContext, tenant.OrganizationId, cancellationToken))
            .FirstOrDefault(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (item is null)
        {
            return Results.NotFound();
        }

        if (!string.Equals(item.ConcurrencyToken, concurrencyToken, StringComparison.Ordinal))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["concurrencyToken"] = ["Exception was modified by another request. Reload and try again."]
            }, statusCode: StatusCodes.Status409Conflict);
        }

        var now = DateTimeOffset.UtcNow;
        var state = await dbContext.OperationsExceptionStates
            .FirstOrDefaultAsync(x => x.OrganizationId == tenant.OrganizationId && x.ExceptionKey == item.Id, cancellationToken);
        state ??= new OperationsExceptionState(
            tenant.OrganizationId,
            item.Id,
            ParseSourceType(item.SourceType),
            ResolveSourceEntityId(item),
            item.DetectedAtUtc);
        if (state.Id == Guid.Empty)
        {
            throw new InvalidOperationException("Unexpected transient state identifier.");
        }

        if (dbContext.Entry(state).State == EntityState.Detached)
        {
            dbContext.OperationsExceptionStates.Add(state);
        }
        else
        {
            state.Refresh(item.DetectedAtUtc);
        }

        var customResult = await mutateAsync(item, tenant, state, now);
        if (customResult is not null)
        {
            return customResult;
        }

        if (item.Links.AlertId is Guid alertId)
        {
            var alert = await dbContext.OperationalAlerts
                .FirstOrDefaultAsync(x => x.OrganizationId == tenant.OrganizationId && x.Id == alertId, cancellationToken);
            if (alert is not null)
            {
                if (auditAction.EndsWith("assigned", StringComparison.Ordinal))
                {
                    alert.Assign(state.AssignedToUserId ?? Guid.Empty, state.AssignedToDisplayName ?? "Unknown user", now);
                }
                else if (auditAction.EndsWith("acknowledged", StringComparison.Ordinal))
                {
                    alert.Acknowledge(state.AcknowledgedByUserId ?? Guid.Empty, state.AcknowledgedByDisplayName ?? "Unknown user", now);
                }
                else if (auditAction.EndsWith("resolved", StringComparison.Ordinal))
                {
                    alert.Resolve(now);
                }
            }
        }

        if (item.Links.SyncIncidentId is Guid syncIncidentId && auditAction.EndsWith("resolved", StringComparison.Ordinal))
        {
            var incident = await dbContext.DriverSyncExceptionIncidents
                .FirstOrDefaultAsync(x => x.OrganizationId == tenant.OrganizationId && x.Id == syncIncidentId, cancellationToken);
            incident?.Resolve(now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await auditService.WriteAsync(
            tenant.OrganizationId,
            tenant.UserId,
            auditAction,
            "operations_exception",
            item.Id,
            auditMetadata,
            cancellationToken);
        await notifier.NotifyQueueChangedAsync(tenant.OrganizationId, auditAction, cancellationToken);
        return Results.Ok();
    }

    private static async Task<List<OperationsExceptionListItemResponse>> BuildQueueAsync(
        FleetOpsDbContext dbContext,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var missions = await dbContext.Missions
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId)
            .ToListAsync(cancellationToken);
        var drivers = await dbContext.Drivers
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId)
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        var vehicles = await dbContext.Vehicles
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId)
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        var alerts = await dbContext.OperationalAlerts
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId)
            .ToListAsync(cancellationToken);
        var alertNotifications = (await dbContext.AlertNotifications
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId)
            .ToListAsync(cancellationToken))
            .GroupBy(x => x.AlertId)
            .ToDictionary(
                x => x.Key,
                x => x
                    .OrderByDescending(y => y.SentAtUtc)
                    .GroupBy(y => y.Channel)
                    .Select(y => y.First())
                    .ToList());
        var inspections = await dbContext.PreDepartureInspections
            .AsNoTracking()
            .Include(x => x.Items)
            .Where(x => x.OrganizationId == organizationId)
            .OrderByDescending(x => x.CompletedAtUtc)
            .ToListAsync(cancellationToken);
        var states = await dbContext.OperationsExceptionStates
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId)
            .ToDictionaryAsync(x => x.ExceptionKey, cancellationToken);
        var syncIncidents = await dbContext.DriverSyncExceptionIncidents
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.ResolvedAtUtc == null)
            .ToListAsync(cancellationToken);

        var items = new List<OperationsExceptionListItemResponse>();

        foreach (var alert in alerts)
        {
            var key = $"alert:{alert.Id:D}";
            states.TryGetValue(key, out var state);
            var notificationSummary = alertNotifications.TryGetValue(alert.Id, out var channels)
                ? string.Join(", ", channels.Select(x => x.Channel.ToString()))
                : "in-app";
            items.Add(BuildItem(
                key,
                "Alert",
                alert.Severity.ToString(),
                state,
                alert.LastDetectedAtUtc,
                alert.RowVersion,
                $"Alert: {alert.Title}",
                $"{alert.Message} Notifications: {notificationSummary}.",
                new OperationsExceptionLinkResponse(
                    null,
                    null,
                    alert.TargetType.Equals("vehicle", StringComparison.OrdinalIgnoreCase) ? alert.TargetEntityId : null,
                    alert.TargetType.Equals("vehicle", StringComparison.OrdinalIgnoreCase) && vehicles.TryGetValue(alert.TargetEntityId, out var vehicle) ? vehicle.RegistrationNumber : null,
                    alert.TargetType.Equals("driver", StringComparison.OrdinalIgnoreCase) ? alert.TargetEntityId : null,
                    alert.TargetType.Equals("driver", StringComparison.OrdinalIgnoreCase) && drivers.TryGetValue(alert.TargetEntityId, out var driver) ? driver.FullName : null,
                    alert.Id,
                    null,
                    null),
                $"{alert.Title} {alert.Message} {alert.TargetType}"));
        }

        foreach (var mission in missions.Where(x => x.Status == MissionStatus.Delayed || x.SimulatedDelayMinutes > 0))
        {
            var key = $"delay:{mission.Id:D}";
            states.TryGetValue(key, out var state);
            var severity = mission.SimulatedDelayMinutes >= 30 ? "Critical" : "Warning";
            items.Add(BuildItem(
                key,
                "MissionDelay",
                severity,
                state,
                mission.CreatedAtUtc,
                mission.RowVersion,
                $"Mission {mission.Reference} delayed",
                $"The mission is delayed by {mission.SimulatedDelayMinutes} minute(s).",
                new OperationsExceptionLinkResponse(
                    mission.Id,
                    mission.Reference,
                    mission.VehicleId,
                    mission.VehicleId is Guid vehicleId && vehicles.TryGetValue(vehicleId, out var vehicle) ? vehicle.RegistrationNumber : null,
                    mission.DriverId,
                    mission.DriverId is Guid driverId && drivers.TryGetValue(driverId, out var driver) ? driver.FullName : null,
                    null,
                    null,
                    null),
                $"{mission.Reference} {mission.Title} delayed"));
        }

        foreach (var inspection in inspections.Where(x => x.HasBlockingCriticalDefect))
        {
            var mission = missions.FirstOrDefault(x => x.Id == inspection.MissionId);
            if (mission is null)
            {
                continue;
            }

            var key = $"defect:{inspection.Id:D}";
            states.TryGetValue(key, out var state);
            var defectLabels = inspection.Items
                .Where(x => !x.IsPass && x.DefectSeverity == DefectSeverity.Critical)
                .Select(x => x.Label)
                .ToList();
            items.Add(BuildItem(
                key,
                "CriticalDefect",
                "Critical",
                state,
                inspection.CompletedAtUtc,
                inspection.Items.Count,
                $"Critical defect on {mission.Reference}",
                $"Mission start is blocked by critical defect(s): {string.Join(", ", defectLabels)}.",
                new OperationsExceptionLinkResponse(
                    mission.Id,
                    mission.Reference,
                    mission.VehicleId,
                    mission.VehicleId is Guid vehicleId && vehicles.TryGetValue(vehicleId, out var vehicle) ? vehicle.RegistrationNumber : null,
                    inspection.DriverId,
                    drivers.TryGetValue(inspection.DriverId, out var driver) ? driver.FullName : null,
                    null,
                    inspection.Id,
                    null),
                $"{mission.Reference} critical defect {string.Join(' ', defectLabels)}"));
        }

        foreach (var incident in syncIncidents)
        {
            var mission = missions.FirstOrDefault(x => x.Id == incident.MissionId);
            if (mission is null)
            {
                continue;
            }

            var key = $"sync:{incident.Id:D}";
            states.TryGetValue(key, out var state);
            items.Add(BuildItem(
                key,
                "DriverSync",
                incident.Severity,
                state,
                incident.LastOccurredAtUtc,
                incident.RowVersion,
                $"Blocked driver sync on {mission.Reference}",
                $"{incident.Message} ({incident.OccurrenceCount} occurrence(s)).",
                new OperationsExceptionLinkResponse(
                    mission.Id,
                    mission.Reference,
                    mission.VehicleId,
                    mission.VehicleId is Guid vehicleId && vehicles.TryGetValue(vehicleId, out var vehicle) ? vehicle.RegistrationNumber : null,
                    incident.DriverId,
                    drivers.TryGetValue(incident.DriverId, out var driver) ? driver.FullName : null,
                    null,
                    null,
                    incident.Id),
                $"{mission.Reference} blocked sync {incident.Message}"));
        }

        return items
            .Where(x => x.WorkflowStatus != "Resolved")
            .OrderByDescending(GetPriority)
            .ThenByDescending(x => x.DetectedAtUtc)
            .ToList();
    }

    private static int GetPriority(OperationsExceptionListItemResponse item)
    {
        var severityScore = item.Severity switch
        {
            "Critical" => 300,
            "Warning" => 200,
            _ => 100,
        };
        var sourceScore = item.SourceType switch
        {
            "CriticalDefect" => 40,
            "DriverSync" => 30,
            "MissionDelay" => 20,
            _ => 10,
        };
        var assignmentPenalty = item.AssignedToUserId is null ? 10 : 0;
        return severityScore + sourceScore + assignmentPenalty;
    }

    private static OperationsExceptionListItemResponse BuildItem(
        string id,
        string sourceType,
        string severity,
        OperationsExceptionState? state,
        DateTimeOffset detectedAtUtc,
        long sourceRowVersion,
        string title,
        string message,
        OperationsExceptionLinkResponse links,
        string searchText)
    {
        var resolved = state?.IsResolvedFor(detectedAtUtc) == true;
        var workflowStatus = resolved
            ? "Resolved"
            : state?.SnoozedUntilUtc is not null && state.SnoozedUntilUtc > DateTimeOffset.UtcNow
                ? "Snoozed"
                : state?.AcknowledgedAtUtc is not null
                    ? "Acknowledged"
                    : "Open";

        var stateRowVersion = state?.RowVersion ?? 0;
        var token = $"{sourceRowVersion}:{stateRowVersion}:{detectedAtUtc.ToUnixTimeSeconds()}";

        return new OperationsExceptionListItemResponse(
            id,
            sourceType,
            severity,
            workflowStatus,
            title,
            message,
            detectedAtUtc,
            state?.SnoozedUntilUtc,
            state?.SnoozeReason,
            state?.ResolvedAtUtc,
            state?.ResolutionReason,
            state?.AssignedToUserId,
            state?.AssignedToDisplayName,
            state?.AcknowledgedByUserId,
            state?.AcknowledgedByDisplayName,
            searchText,
            sourceRowVersion,
            stateRowVersion,
            token,
            links);
    }

    private static Guid ResolveSourceEntityId(OperationsExceptionListItemResponse item) =>
        item.Links.AlertId
        ?? item.Links.InspectionId
        ?? item.Links.SyncIncidentId
        ?? item.Links.MissionId
        ?? throw new InvalidOperationException("Operations exception item does not expose a source entity.");

    private static OperationsExceptionSourceType ParseSourceType(string sourceType) =>
        sourceType switch
        {
            "Alert" => OperationsExceptionSourceType.Alert,
            "MissionDelay" => OperationsExceptionSourceType.MissionDelay,
            "CriticalDefect" => OperationsExceptionSourceType.CriticalDefect,
            "DriverSync" => OperationsExceptionSourceType.DriverSync,
            _ => throw new InvalidOperationException($"Unknown operations exception source type '{sourceType}'."),
        };

    private static async Task AppendTimelineAsync(
        OperationsExceptionListItemResponse item,
        Guid organizationId,
        FleetOpsDbContext dbContext,
        string description,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken)
    {
        if (item.Links.MissionId is not Guid missionId)
        {
            return;
        }

        dbContext.MissionTimelineEvents.Add(new MissionTimelineEvent(
            organizationId,
            missionId,
            MissionTimelineEventType.Updated,
            description,
            occurredAtUtc));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static OperationsSavedViewResponse MapSavedView(OperationsSavedView entity)
    {
        var filters = JsonSerializer.Deserialize<OperationsSavedViewFilterRequest>(entity.FilterJson)
            ?? new OperationsSavedViewFilterRequest(null, null, null, null, null, false);
        return new OperationsSavedViewResponse(
            entity.Id,
            entity.Name,
            entity.IsShared,
            filters,
            entity.RowVersion,
            entity.CreatedByUserId);
    }
}
