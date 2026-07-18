using FleetOps.Api.Auditing;
using FleetOps.Api.Media;
using FleetOps.Api.Operations;
using FleetOps.Api.Security;
using FleetOps.Core.Modules.Compliance;
using FleetOps.Core.Modules.Dispatch;
using FleetOps.Core.Modules.Identity;
using FleetOps.Core.Modules.Operations;
using FleetOps.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace FleetOps.Api.DriverApp;

public static class DriverAppEndpointExtensions
{
    public static IEndpointRouteBuilder MapDriverAppEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/driver")
            .RequireAuthorization(AuthorizationPolicies.DriverOnly);

        group.MapGet("/missions", ListAssignedMissionsAsync);
        group.MapGet("/missions/{id:guid}", GetAssignedMissionAsync);
        group.MapPost("/missions/{id:guid}/commands", SyncMissionCommandAsync);
        group.MapGet("/compliance-campaign-tasks", ListComplianceCampaignTasksAsync);
        group.MapPost("/compliance-campaign-tasks/{id:guid}/submit", SubmitComplianceCampaignTaskAsync);

        return app;
    }

    private static async Task<IResult> ListComplianceCampaignTasksAsync(HttpContext httpContext, FleetOpsDbContext dbContext, ICurrentTenantAccessor currentTenantAccessor, CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User); if (tenant.DriverId is not Guid driverId) return Results.Forbid();
        var now = DateTimeOffset.UtcNow;
        var tasks = await (from task in dbContext.ComplianceInspectionCampaignTasks
                           join campaign in dbContext.ComplianceInspectionCampaigns on task.CampaignId equals campaign.Id
                           join vehicle in dbContext.Vehicles on task.VehicleId equals vehicle.Id
                           where task.OrganizationId == tenant.OrganizationId && task.DriverId == driverId && campaign.Status == InspectionCampaignStatus.Active && campaign.ClosesAtUtc >= now
                           orderby campaign.ClosesAtUtc
                           select new DriverComplianceCampaignTaskResponse(task.Id, task.VehicleId, vehicle.RegistrationNumber, campaign.Name, task.TemplateCode, campaign.OpensAtUtc, campaign.ClosesAtUtc, task.Status)).ToListAsync(cancellationToken);
        return Results.Ok(tasks);
    }

    private static async Task<IResult> SubmitComplianceCampaignTaskAsync(Guid id, SubmitDriverComplianceCampaignTaskRequest request, HttpContext httpContext, FleetOpsDbContext dbContext, ICurrentTenantAccessor currentTenantAccessor, IAuditService auditService, CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User); if (tenant.DriverId is not Guid driverId) return Results.Forbid();
        if (string.IsNullOrWhiteSpace(request.CommandId) || request.CommandId.Trim().Length > 120) return Results.ValidationProblem(new Dictionary<string, string[]> { ["commandId"] = ["A command identifier of up to 120 characters is required."] });
        var task = await dbContext.ComplianceInspectionCampaignTasks.FirstOrDefaultAsync(x => x.OrganizationId == tenant.OrganizationId && x.Id == id && x.DriverId == driverId, cancellationToken); if (task is null) return Results.NotFound();
        var campaign = await dbContext.ComplianceInspectionCampaigns.FirstOrDefaultAsync(x => x.OrganizationId == tenant.OrganizationId && x.Id == task.CampaignId && x.Status == InspectionCampaignStatus.Active, cancellationToken); if (campaign is null || campaign.ClosesAtUtc < DateTimeOffset.UtcNow) return Results.ValidationProblem(new Dictionary<string, string[]> { ["campaign"] = ["Campaign is not active."] }, statusCode: StatusCodes.Status409Conflict);
        var duplicate = await dbContext.ComplianceInspectionCampaignTasks.AnyAsync(x => x.OrganizationId == tenant.OrganizationId && x.SubmissionCommandId == request.CommandId.Trim(), cancellationToken); if (!duplicate) { task.Submit(request.CommandId, request.SubmittedAtUtc, request.Notes); await dbContext.SaveChangesAsync(cancellationToken); await auditService.WriteAsync(tenant.OrganizationId, tenant.UserId, "driver.compliance_campaign_submitted", "compliance-campaign-task", task.Id.ToString(), new { task.CampaignId, task.VehicleId, request.CommandId }, cancellationToken); }
        return Results.Ok(new { task.Id, task.Status, WasDuplicate = duplicate });
    }

    private static async Task<IResult> ListAssignedMissionsAsync(
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        if (tenant.DriverId is not Guid driverId)
        {
            return Results.Forbid();
        }

        var vehicleNames = await dbContext.Vehicles
            .Where(x => x.OrganizationId == tenant.OrganizationId)
            .ToDictionaryAsync(x => x.Id, x => x.RegistrationNumber, cancellationToken);

        var missions = await dbContext.Missions
            .Where(x => x.OrganizationId == tenant.OrganizationId && x.DriverId == driverId)
            .OrderBy(x => x.ScheduledStartUtc)
            .Select(x => new
            {
                x.Id,
                x.Reference,
                x.Title,
                x.Status,
                x.ScheduledStartUtc,
                x.ScheduledEndUtc,
                x.VehicleId,
                StopCount = x.Stops.Count,
                x.RowVersion
            })
            .ToListAsync(cancellationToken);

        return Results.Ok(missions.Select(mission => new DriverMissionSummaryResponse(
            mission.Id,
            mission.Reference,
            mission.Title,
            mission.Status,
            mission.ScheduledStartUtc,
            mission.ScheduledEndUtc,
            mission.VehicleId is Guid vehicleId && vehicleNames.TryGetValue(vehicleId, out var vehicleName)
                ? vehicleName
                : null,
            mission.StopCount,
            // Pending commands exist only in the device outbox. Server receipts represent
            // already processed commands and must never be surfaced as pending work.
            0,
            mission.RowVersion)));
    }

    private static async Task<IResult> GetAssignedMissionAsync(
        Guid id,
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        if (tenant.DriverId is not Guid driverId)
        {
            return Results.Forbid();
        }

        var mission = await LoadAssignedMissionAsync(id, tenant.OrganizationId, driverId, dbContext, cancellationToken);
        return mission is null ? Results.NotFound() : Results.Ok(mission);
    }

    private static async Task<IResult> SyncMissionCommandAsync(
        Guid id,
        SyncMissionCommandRequest request,
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        IAuditService auditService,
        IDriverSyncIncidentService driverSyncIncidentService,
        IOperationsRealtimeNotifier notifier,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        if (tenant.DriverId is not Guid driverId)
        {
            return Results.Forbid();
        }

        var commandId = request.CommandId?.Trim() ?? string.Empty;
        if (commandId.Length == 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["commandId"] = ["Command identifier is required."]
            });
        }

        var existingReceipt = await dbContext.DriverSyncCommandReceipts
            .FirstOrDefaultAsync(
                x => x.OrganizationId == tenant.OrganizationId && x.CommandId == commandId,
                cancellationToken);

        if (existingReceipt is not null)
        {
            var duplicateMission = await LoadAssignedMissionAsync(id, tenant.OrganizationId, driverId, dbContext, cancellationToken);
            return duplicateMission is null
                ? Results.NotFound()
                : Results.Ok(new SyncMissionCommandResponse(duplicateMission, WasDuplicate: true));
        }

        var mission = await dbContext.Missions
            .Include(x => x.Stops)
            .Include(x => x.Timeline)
            .FirstOrDefaultAsync(
                x => x.OrganizationId == tenant.OrganizationId && x.Id == id && x.DriverId == driverId,
                cancellationToken);
        if (mission is null)
        {
            return Results.NotFound();
        }

        if (mission.RowVersion != request.RowVersion)
        {
            await driverSyncIncidentService.RecordAsync(
                tenant.OrganizationId,
                mission.Id,
                driverId,
                "mission-command",
                "stale-row-version",
                "Warning",
                "The driver app tried to sync a stale mission version.",
                request.CommandId,
                cancellationToken);
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["rowVersion"] = ["Mission was modified by another request. Reload and retry sync."]
            }, statusCode: StatusCodes.Status409Conflict);
        }

        try
        {
            if (request.Action == DriverMissionCommandAction.Start)
            {
                var latestInspection = await DriverOperationsEndpointExtensions.LoadLatestInspectionAsync(
                    mission.Id,
                    tenant.OrganizationId,
                    dbContext,
                    new NoOpMediaUrlSigner(),
                    cancellationToken);
                if (latestInspection is null)
                {
                    await driverSyncIncidentService.RecordAsync(
                        tenant.OrganizationId,
                        mission.Id,
                        driverId,
                        "mission-command",
                        "inspection-missing",
                        "Warning",
                        "Mission start was blocked because no pre-departure inspection was available.",
                        request.CommandId,
                        cancellationToken);
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["inspection"] = ["A pre-departure inspection must be completed before mission start."]
                    }, statusCode: StatusCodes.Status409Conflict);
                }

                if (latestInspection.HasBlockingCriticalDefect)
                {
                    await driverSyncIncidentService.RecordAsync(
                        tenant.OrganizationId,
                        mission.Id,
                        driverId,
                        "mission-command",
                        "critical-defect-block",
                        "Critical",
                        "Mission start was blocked by a critical inspection defect.",
                        request.CommandId,
                        cancellationToken);
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["inspection"] = ["Mission start is blocked by a critical inspection defect."]
                    }, statusCode: StatusCodes.Status409Conflict);
                }
            }

            mission.TransitionTo(ToMissionStatus(request.Action), request.OccurredAtUtc);
            dbContext.MissionTimelineEvents.Add(mission.Timeline.OrderByDescending(x => x.OccurredAtUtc).First());
            dbContext.DriverSyncCommandReceipts.Add(new DriverSyncCommandReceipt(
                tenant.OrganizationId,
                driverId,
                mission.Id,
                commandId,
                request.Action,
                request.OccurredAtUtc));
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (ArgumentException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [ex.ParamName ?? "body"] = [ex.Message]
            });
        }
        catch (InvalidOperationException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["action"] = [ex.Message]
            });
        }
        catch (DbUpdateException)
        {
            var duplicateMission = await LoadAssignedMissionAsync(id, tenant.OrganizationId, driverId, dbContext, cancellationToken);
            return duplicateMission is null
                ? Results.NotFound()
                : Results.Ok(new SyncMissionCommandResponse(duplicateMission, WasDuplicate: true));
        }

        await auditService.WriteAsync(
            tenant.OrganizationId,
            tenant.UserId,
            "driver.mission_command_synced",
            "mission",
            mission.Id.ToString(),
            new { request.Action, request.CommandId, mission.Status },
            cancellationToken);
        await notifier.NotifyQueueChangedAsync(tenant.OrganizationId, "driver.mission_command_synced", cancellationToken);

        var updatedMission = await LoadAssignedMissionAsync(id, tenant.OrganizationId, driverId, dbContext, cancellationToken);
        return Results.Ok(new SyncMissionCommandResponse(updatedMission!, WasDuplicate: false));
    }

    private sealed class NoOpMediaUrlSigner : IMediaUrlSigner
    {
        public string CreateReadUrl(Guid assetId, Guid organizationId, TimeSpan lifetime) => string.Empty;

        public bool IsValid(Guid assetId, Guid organizationId, long expiresUnixSeconds, string signature) => false;
    }

    private static MissionStatus ToMissionStatus(DriverMissionCommandAction action) =>
        action switch
        {
            DriverMissionCommandAction.Start => MissionStatus.EnRoute,
            DriverMissionCommandAction.Arrive => MissionStatus.Arrived,
            DriverMissionCommandAction.Complete => MissionStatus.Completed,
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unsupported driver command action.")
        };

    private static async Task<DriverMissionDetailResponse?> LoadAssignedMissionAsync(
        Guid missionId,
        Guid organizationId,
        Guid driverId,
        FleetOpsDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var mission = await dbContext.Missions
            .Include(x => x.Stops)
            .Include(x => x.Timeline)
            .FirstOrDefaultAsync(
                x => x.OrganizationId == organizationId && x.Id == missionId && x.DriverId == driverId,
                cancellationToken);
        if (mission is null)
        {
            return null;
        }

        var vehicleRegistrationNumber = mission.VehicleId is Guid vehicleId
            ? await dbContext.Vehicles
                .Where(x => x.OrganizationId == organizationId && x.Id == vehicleId)
                .Select(x => x.RegistrationNumber)
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        return new DriverMissionDetailResponse(
            mission.Id,
            mission.Reference,
            mission.Title,
            mission.Status,
            mission.ScheduledStartUtc,
            mission.ScheduledEndUtc,
            vehicleRegistrationNumber,
            mission.SimulatedDelayMinutes,
            mission.RowVersion,
            mission.Stops
                .OrderBy(x => x.Sequence)
                .Select(x => new DriverMissionStopResponse(
                    x.Id,
                    x.Sequence,
                    x.Name,
                    x.Address,
                    x.PlannedArrivalUtc))
                .ToList(),
            mission.Timeline
                .OrderBy(x => x.OccurredAtUtc)
                .Select(x => new DriverMissionTimelineEventResponse(
                    x.Id,
                    x.EventType,
                    x.Description,
                    x.OccurredAtUtc))
                .ToList());
    }
}
