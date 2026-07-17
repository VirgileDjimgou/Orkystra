using FleetOps.Api.Auditing;
using FleetOps.Api.Integrations;
using FleetOps.Api.Media;
using FleetOps.Api.Operations;
using FleetOps.Api.Security;
using FleetOps.Core.Modules.Alerts;
using FleetOps.Core.Modules.Compliance;
using FleetOps.Core.Modules.Dispatch;
using FleetOps.Core.Modules.Identity;
using FleetOps.Core.Modules.Integrations;
using FleetOps.Core.Modules.Maintenance;
using FleetOps.Infrastructure.Integrations;
using FleetOps.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace FleetOps.Api.Dispatch;

public static class DispatchEndpointExtensions
{
    private const string DispatcherRoles = SystemRoles.Admin + "," + SystemRoles.Operator;

    public static IEndpointRouteBuilder MapDispatchEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/dispatch/missions")
            .RequireAuthorization(new AuthorizeAttribute { Roles = DispatcherRoles });

        group.MapGet("/", ListMissionsAsync);
        group.MapGet("/{id:guid}", GetMissionAsync);
        group.MapGet("/{id:guid}/timeline", GetTimelineAsync);
        group.MapPost("/", CreateMissionAsync);
        group.MapPut("/{id:guid}", UpdateMissionAsync);
        group.MapPut("/{id:guid}/assignment", SetAssignmentAsync);
        group.MapPost("/{id:guid}/status", TransitionStatusAsync);
        group.MapPost("/{id:guid}/delay-simulation", SimulateDelayAsync);

        return app;
    }

    private static async Task<IResult> ListMissionsAsync(
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);

        var missions = await dbContext.Missions
            .Where(x => x.OrganizationId == tenant.OrganizationId)
            .OrderByDescending(x => x.ScheduledStartUtc)
            .Select(x => new MissionSummaryResponse(
                x.Id,
                x.Reference,
                x.Title,
                x.Status,
                x.ScheduledStartUtc,
                x.ScheduledEndUtc,
                x.DriverId,
                null,
                x.VehicleId,
                null,
                x.Stops.Count,
                x.SimulatedDelayMinutes,
                x.RowVersion,
                null,
                null))
            .ToListAsync(cancellationToken);

        if (missions.Count == 0)
        {
            return Results.Ok(missions);
        }

        var missionIds = missions.Select(x => x.Id).ToList();
        var driverNames = await dbContext.Drivers
            .Where(x => x.OrganizationId == tenant.OrganizationId)
            .ToDictionaryAsync(x => x.Id, x => x.FullName, cancellationToken);
        var vehicleNames = await dbContext.Vehicles
            .Where(x => x.OrganizationId == tenant.OrganizationId)
            .ToDictionaryAsync(x => x.Id, x => x.RegistrationNumber, cancellationToken);
        var positions = await dbContext.CurrentVehiclePositions
            .Where(x => x.OrganizationId == tenant.OrganizationId)
            .ToDictionaryAsync(x => x.VehicleId, cancellationToken);

        var enriched = missions.Select(mission =>
        {
            positions.TryGetValue(mission.VehicleId ?? Guid.Empty, out var position);
            return mission with
            {
                DriverName = mission.DriverId is Guid driverId && driverNames.TryGetValue(driverId, out var driverName)
                    ? driverName
                    : null,
                VehicleRegistrationNumber = mission.VehicleId is Guid vehicleId && vehicleNames.TryGetValue(vehicleId, out var registration)
                    ? registration
                    : null,
                CurrentLatitude = position?.Latitude,
                CurrentLongitude = position?.Longitude,
            };
        });

        return Results.Ok(enriched);
    }

    private static async Task<IResult> GetMissionAsync(
        Guid id,
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        IMediaUrlSigner signer,
        ICurrentTenantAccessor currentTenantAccessor,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        var mission = await LoadMissionAggregateAsync(id, tenant.OrganizationId, dbContext, signer, cancellationToken);
        return mission is null ? Results.NotFound() : Results.Ok(mission);
    }

    private static async Task<IResult> GetTimelineAsync(
        Guid id,
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        var missionExists = await dbContext.Missions.AnyAsync(
            x => x.OrganizationId == tenant.OrganizationId && x.Id == id,
            cancellationToken);
        if (!missionExists)
        {
            return Results.NotFound();
        }

        var timeline = await dbContext.MissionTimelineEvents
            .Where(x => x.OrganizationId == tenant.OrganizationId && x.MissionId == id)
            .OrderBy(x => x.OccurredAtUtc)
            .Select(x => new MissionTimelineEventResponse(
                x.Id,
                x.EventType,
                x.Description,
                x.OccurredAtUtc))
            .ToListAsync(cancellationToken);

        return Results.Ok(timeline);
    }

    private static async Task<IResult> CreateMissionAsync(
        CreateMissionRequest request,
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        IMediaUrlSigner signer,
        ICurrentTenantAccessor currentTenantAccessor,
        IAuditService auditService,
        IIntegrationOutboxService integrationOutboxService,
        IOperationsRealtimeNotifier notifier,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        var reference = request.Reference?.Trim() ?? string.Empty;
        if (await dbContext.Missions.AnyAsync(
                x => x.OrganizationId == tenant.OrganizationId && x.Reference == reference,
                cancellationToken))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["reference"] = ["Mission reference already exists in this organization."]
            });
        }

        Mission mission;
        try
        {
            mission = new Mission(
                tenant.OrganizationId,
                reference,
                request.Title,
                request.ScheduledStartUtc,
                request.ScheduledEndUtc);
            mission.ReplaceStops(BuildStops(tenant.OrganizationId, mission.Id, request.Stops));
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
                ["mission"] = [ex.Message]
            });
        }

        dbContext.Missions.Add(mission);
        dbContext.MissionStops.AddRange(mission.Stops);
        dbContext.MissionTimelineEvents.AddRange(mission.Timeline);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(
            tenant.OrganizationId,
            tenant.UserId,
            "dispatch.mission_created",
            "mission",
            mission.Id.ToString(),
            new { mission.Reference, mission.Status },
            cancellationToken);
        await notifier.NotifyQueueChangedAsync(tenant.OrganizationId, "dispatch.mission_created", cancellationToken);

        return Results.Created(
            $"/api/v1/dispatch/missions/{mission.Id}",
            await LoadMissionAggregateAsync(mission.Id, tenant.OrganizationId, dbContext, signer, cancellationToken));
    }

    private static async Task<IResult> UpdateMissionAsync(
        Guid id,
        UpdateMissionRequest request,
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        IMediaUrlSigner signer,
        ICurrentTenantAccessor currentTenantAccessor,
        IAuditService auditService,
        IOperationsRealtimeNotifier notifier,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        var mission = await dbContext.Missions
            .Include(x => x.Stops)
            .Include(x => x.Timeline)
            .FirstOrDefaultAsync(x => x.OrganizationId == tenant.OrganizationId && x.Id == id, cancellationToken);
        if (mission is null)
        {
            return Results.NotFound();
        }

        if (mission.RowVersion != request.RowVersion)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["rowVersion"] = ["Mission was modified by another request. Reload and try again."]
            }, statusCode: StatusCodes.Status409Conflict);
        }

        try
        {
            mission.UpdateDetails(request.Title, request.ScheduledStartUtc, request.ScheduledEndUtc);
            dbContext.MissionStops.RemoveRange(mission.Stops);
            mission.ReplaceStops(BuildStops(tenant.OrganizationId, mission.Id, request.Stops));
            dbContext.MissionStops.AddRange(mission.Stops);
            dbContext.MissionTimelineEvents.Add(mission.Timeline.OrderByDescending(x => x.OccurredAtUtc).First());
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
                ["mission"] = [ex.Message]
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(
            tenant.OrganizationId,
            tenant.UserId,
            "dispatch.mission_updated",
            "mission",
            mission.Id.ToString(),
            new { mission.Title, mission.RowVersion },
            cancellationToken);
        await notifier.NotifyQueueChangedAsync(tenant.OrganizationId, "dispatch.mission_updated", cancellationToken);

        return Results.Ok(await LoadMissionAggregateAsync(mission.Id, tenant.OrganizationId, dbContext, signer, cancellationToken));
    }

    private static async Task<IResult> SetAssignmentAsync(
        Guid id,
        SetMissionAssignmentRequest request,
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        IMediaUrlSigner signer,
        ICurrentTenantAccessor currentTenantAccessor,
        IAuditService auditService,
        IOperationsRealtimeNotifier notifier,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        var mission = await dbContext.Missions
            .Include(x => x.Stops)
            .Include(x => x.Timeline)
            .FirstOrDefaultAsync(x => x.OrganizationId == tenant.OrganizationId && x.Id == id, cancellationToken);
        if (mission is null)
        {
            return Results.NotFound();
        }

        if (mission.RowVersion != request.RowVersion)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["rowVersion"] = ["Mission was modified by another request. Reload and try again."]
            }, statusCode: StatusCodes.Status409Conflict);
        }

        var driver = await dbContext.Drivers
            .FirstOrDefaultAsync(x => x.OrganizationId == tenant.OrganizationId && x.Id == request.DriverId && x.IsActive, cancellationToken);
        if (driver is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["driverId"] = ["Driver does not exist or is inactive in this organization."]
            });
        }

        var vehicle = await dbContext.Vehicles
            .FirstOrDefaultAsync(x => x.OrganizationId == tenant.OrganizationId && x.Id == request.VehicleId && x.IsActive, cancellationToken);
        if (vehicle is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["vehicleId"] = ["Vehicle does not exist or is inactive in this organization."]
            });
        }

        var unavailable = await dbContext.MaintenanceWorkOrders.AnyAsync(
            x => x.OrganizationId == tenant.OrganizationId && x.VehicleId == request.VehicleId && x.ImmobilizesVehicle
                && x.Status != MaintenanceWorkOrderStatus.Completed && x.Status != MaintenanceWorkOrderStatus.Cancelled,
            cancellationToken);
        if (unavailable)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["vehicleId"] = ["Vehicle is immobilized by an active maintenance work order and cannot be assigned."]
            }, statusCode: StatusCodes.Status409Conflict);
        }

        var compliancePolicy = await dbContext.CompliancePolicies
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.OrganizationId == tenant.OrganizationId, cancellationToken);
        if (compliancePolicy?.BlocksAssignments == true)
        {
            var now = DateTimeOffset.UtcNow;
            var blockingTypes = await dbContext.ComplianceDocumentTypes
                .AsNoTracking()
                .Where(x => x.OrganizationId == tenant.OrganizationId && x.IsActive && x.IsBlocking)
                .ToListAsync(cancellationToken);
            var blocked = blockingTypes.Any(type => !dbContext.ComplianceDocuments.Any(document =>
                document.OrganizationId == tenant.OrganizationId
                && document.TargetEntityId == (type.SubjectType == ComplianceSubjectType.Vehicle ? request.VehicleId : request.DriverId)
                && (document.ComplianceDocumentTypeId == type.Id || document.DocumentType == type.Name)
                && document.ReplacedByDocumentId == null
                && document.ReviewStatus == ComplianceReviewStatus.Approved
                && document.ExpiresAtUtc > now));
            if (blocked && string.IsNullOrWhiteSpace(request.ComplianceOverrideReason))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["compliance"] = ["Assignment is blocked by this organization's compliance policy. An administrator may provide an audited override reason."]
                }, statusCode: StatusCodes.Status409Conflict);
            }
            if (blocked && !httpContext.User.IsInRole(SystemRoles.Admin))
            {
                return Results.Forbid();
            }
            if (blocked && request.ComplianceOverrideReason!.Trim().Length > 280)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["complianceOverrideReason"] = ["Override reason must not exceed 280 characters."] });
            }
        }

        var conflict = await FindConflictAsync(mission, request.DriverId, request.VehicleId, dbContext, cancellationToken);
        if (conflict is not null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["assignment"] = [$"Mission conflicts with {conflict.Reference} in the current schedule window."]
            }, statusCode: StatusCodes.Status409Conflict);
        }

        try
        {
            mission.SetAssignment(request.DriverId, request.VehicleId);
            dbContext.MissionTimelineEvents.Add(mission.Timeline.OrderByDescending(x => x.OccurredAtUtc).First());
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
                ["mission"] = [ex.Message]
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(
            tenant.OrganizationId,
            tenant.UserId,
            "dispatch.mission_assignment_changed",
            "mission",
            mission.Id.ToString(),
            new { mission.DriverId, mission.VehicleId, request.ComplianceOverrideReason },
            cancellationToken);
        await notifier.NotifyQueueChangedAsync(tenant.OrganizationId, "dispatch.mission_assignment_changed", cancellationToken);

        return Results.Ok(await LoadMissionAggregateAsync(mission.Id, tenant.OrganizationId, dbContext, signer, cancellationToken));
    }

    private static async Task<IResult> TransitionStatusAsync(
        Guid id,
        TransitionMissionStatusRequest request,
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        IMediaUrlSigner signer,
        ICurrentTenantAccessor currentTenantAccessor,
        IAuditService auditService,
        IIntegrationOutboxService integrationOutboxService,
        IOperationsRealtimeNotifier notifier,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        var mission = await dbContext.Missions
            .Include(x => x.Stops)
            .Include(x => x.Timeline)
            .FirstOrDefaultAsync(x => x.OrganizationId == tenant.OrganizationId && x.Id == id, cancellationToken);
        if (mission is null)
        {
            return Results.NotFound();
        }

        if (mission.RowVersion != request.RowVersion)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["rowVersion"] = ["Mission was modified by another request. Reload and try again."]
            }, statusCode: StatusCodes.Status409Conflict);
        }

        try
        {
            mission.TransitionTo(request.TargetStatus, DateTimeOffset.UtcNow);
            dbContext.MissionTimelineEvents.Add(mission.Timeline.OrderByDescending(x => x.OccurredAtUtc).First());
        }
        catch (InvalidOperationException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["status"] = [ex.Message]
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(
            tenant.OrganizationId,
            tenant.UserId,
            "dispatch.mission_status_changed",
            "mission",
            mission.Id.ToString(),
            new { mission.Status },
            cancellationToken);

        await integrationOutboxService.PublishAsync(
            tenant.OrganizationId,
            IntegrationEventType.MissionStatusChanged,
            "mission",
            mission.Id.ToString(),
            new
            {
                missionId = mission.Id,
                mission.Reference,
                status = mission.Status.ToString(),
                mission.DriverId,
                mission.VehicleId
            },
            cancellationToken);
        await notifier.NotifyQueueChangedAsync(tenant.OrganizationId, "dispatch.mission_status_changed", cancellationToken);

        return Results.Ok(await LoadMissionAggregateAsync(mission.Id, tenant.OrganizationId, dbContext, signer, cancellationToken));
    }

    private static async Task<IResult> SimulateDelayAsync(
        Guid id,
        SimulateMissionDelayRequest request,
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        IMediaUrlSigner signer,
        ICurrentTenantAccessor currentTenantAccessor,
        IAuditService auditService,
        IOperationsRealtimeNotifier notifier,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        var mission = await dbContext.Missions
            .Include(x => x.Timeline)
            .FirstOrDefaultAsync(x => x.OrganizationId == tenant.OrganizationId && x.Id == id, cancellationToken);
        if (mission is null)
        {
            return Results.NotFound();
        }

        if (mission.RowVersion != request.RowVersion)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["rowVersion"] = ["Mission was modified by another request. Reload and try again."]
            }, statusCode: StatusCodes.Status409Conflict);
        }

        try
        {
            mission.SimulateDelay(request.DelayMinutes, DateTimeOffset.UtcNow);
            dbContext.MissionTimelineEvents.Add(mission.Timeline.OrderByDescending(x => x.OccurredAtUtc).First());
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [ex.ParamName ?? "delayMinutes"] = [ex.Message]
            });
        }
        catch (InvalidOperationException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["mission"] = [ex.Message]
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(
            tenant.OrganizationId,
            tenant.UserId,
            "dispatch.mission_delay_simulated",
            "mission",
            mission.Id.ToString(),
            new { mission.SimulatedDelayMinutes, mission.Status },
            cancellationToken);
        await notifier.NotifyQueueChangedAsync(tenant.OrganizationId, "dispatch.mission_delay_simulated", cancellationToken);

        return Results.Ok(await LoadMissionAggregateAsync(mission.Id, tenant.OrganizationId, dbContext, signer, cancellationToken));
    }

    private static List<MissionStop> BuildStops(
        Guid organizationId,
        Guid missionId,
        IReadOnlyList<MissionStopRequest> requests)
    {
        return requests
            .OrderBy(x => x.Sequence)
            .Select(x => new MissionStop(
                organizationId,
                missionId,
                x.Sequence,
                x.Name,
                x.Address,
                x.PlannedArrivalUtc))
            .ToList();
    }

    private static async Task<Mission?> FindConflictAsync(
        Mission mission,
        Guid driverId,
        Guid vehicleId,
        FleetOpsDbContext dbContext,
        CancellationToken cancellationToken)
    {
        return await dbContext.Missions
            .Where(x => x.OrganizationId == mission.OrganizationId
                && x.Id != mission.Id
                && x.Status != MissionStatus.Completed
                && x.Status != MissionStatus.Cancelled
                && x.Status != MissionStatus.Draft
                && (x.DriverId == driverId || x.VehicleId == vehicleId)
                && x.ScheduledStartUtc < mission.ScheduledEndUtc
                && mission.ScheduledStartUtc < x.ScheduledEndUtc)
            .OrderBy(x => x.ScheduledStartUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static async Task<MissionDetailResponse?> LoadMissionAggregateAsync(
        Guid missionId,
        Guid organizationId,
        FleetOpsDbContext dbContext,
        IMediaUrlSigner signer,
        CancellationToken cancellationToken)
    {
        var mission = await dbContext.Missions
            .Include(x => x.Stops)
            .Include(x => x.Timeline)
            .FirstOrDefaultAsync(
                x => x.OrganizationId == organizationId && x.Id == missionId,
                cancellationToken);
        if (mission is null)
        {
            return null;
        }

        var driverName = mission.DriverId is Guid driverId
            ? await dbContext.Drivers
                .Where(x => x.OrganizationId == organizationId && x.Id == driverId)
                .Select(x => x.FullName)
                .FirstOrDefaultAsync(cancellationToken)
            : null;
        var vehicleRegistration = mission.VehicleId is Guid vehicleId
            ? await dbContext.Vehicles
                .Where(x => x.OrganizationId == organizationId && x.Id == vehicleId)
                .Select(x => x.RegistrationNumber)
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        var latestInspection = await DriverOperationsEndpointExtensions.LoadLatestInspectionAsync(
            mission.Id,
            organizationId,
            dbContext,
            signer,
            cancellationToken);
        var deliveryProofs = new List<MissionStopProofResponse>();
        foreach (var stop in mission.Stops.OrderBy(x => x.Sequence))
        {
            var proof = await DriverOperationsEndpointExtensions.LoadDeliveryProofAsync(
                mission.Id,
                stop.Id,
                organizationId,
                dbContext,
                signer,
                cancellationToken);
            if (proof is not null)
            {
                deliveryProofs.Add(new MissionStopProofResponse(
                    proof.ProofId,
                    proof.MissionStopId,
                    proof.RecipientName,
                    proof.SignatureName,
                    proof.DeliveredAtUtc,
                    proof.Notes,
                    proof.Photos
                        .Select(x => new MissionProofPhotoResponse(x.MediaAssetId, x.Caption, x.Photo.ReadUrl))
                        .ToList()));
            }
        }

        return new MissionDetailResponse(
            mission.Id,
            mission.Reference,
            mission.Title,
            mission.Status,
            mission.ScheduledStartUtc,
            mission.ScheduledEndUtc,
            mission.DriverId,
            driverName,
            mission.VehicleId,
            vehicleRegistration,
            mission.SimulatedDelayMinutes,
            mission.RowVersion,
            latestInspection is null
                ? null
                : new MissionInspectionResponse(
                    latestInspection.InspectionId,
                    latestInspection.Outcome,
                    latestInspection.HasBlockingCriticalDefect,
                    latestInspection.CompletedAtUtc,
                    latestInspection.Notes,
                    latestInspection.Items
                        .Select(x => new MissionInspectionItemResponse(
                            x.Sequence,
                            x.Code,
                            x.Label,
                            x.IsPass,
                            x.DefectSeverity,
                            x.Notes,
                            x.Photo?.ReadUrl))
                        .ToList()),
            deliveryProofs,
            mission.Stops
                .OrderBy(x => x.Sequence)
                .Select(x => new MissionStopResponse(
                    x.Id,
                    x.Sequence,
                    x.Name,
                    x.Address,
                    x.PlannedArrivalUtc))
                .ToList(),
            mission.Timeline
                .OrderBy(x => x.OccurredAtUtc)
                .Select(x => new MissionTimelineEventResponse(
                    x.Id,
                    x.EventType,
                    x.Description,
                    x.OccurredAtUtc))
                .ToList());
    }
}
