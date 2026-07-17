using FleetOps.Api.Auditing;
using FleetOps.Api.Media;
using FleetOps.Api.Operations;
using FleetOps.Api.Security;
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

        return app;
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

        var missionIdsWithPendingCommands = await dbContext.DriverSyncCommandReceipts
            .Where(x => x.OrganizationId == tenant.OrganizationId && x.DriverId == driverId)
            .GroupBy(x => x.MissionId)
            .Select(x => new { MissionId = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.MissionId, x => x.Count, cancellationToken);

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
            missionIdsWithPendingCommands.GetValueOrDefault(mission.Id),
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
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["inspection"] = ["A pre-departure inspection must be completed before mission start."]
                    }, statusCode: StatusCodes.Status409Conflict);
                }

                if (latestInspection.HasBlockingCriticalDefect)
                {
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

        var updatedMission = await LoadAssignedMissionAsync(id, tenant.OrganizationId, driverId, dbContext, cancellationToken);
        return Results.Ok(new SyncMissionCommandResponse(updatedMission!, WasDuplicate: false));
    }

    private sealed class NoOpMediaUrlSigner : IMediaUrlSigner
    {
        public string CreateReadUrl(Guid assetId, TimeSpan lifetime) => string.Empty;

        public bool IsValid(Guid assetId, long expiresUnixSeconds, string signature) => false;
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
