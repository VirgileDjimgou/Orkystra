using FleetOps.Api.Auditing;
using FleetOps.Api.Operations;
using FleetOps.Api.Security;
using FleetOps.Core.Modules.Demo;
using FleetOps.Core.Modules.Identity;
using FleetOps.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace FleetOps.Api.Demo;

public static class AgentActivityEndpointExtensions
{
    private const string ReaderRoles = SystemRoles.Admin + "," + SystemRoles.Operator;

    public static IEndpointRouteBuilder MapAgentActivityEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/demo/agent-activities")
            .RequireAuthorization(new AuthorizeAttribute { Roles = ReaderRoles });
        group.MapGet("/", ListAsync);
        group.MapPost("/", RecordAsync);
        return app;
    }

    private static async Task<IResult> ListAsync(
        int? take,
        HttpContext context,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor tenants,
        CancellationToken cancellationToken)
    {
        var tenant = tenants.GetRequiredTenant(context.User);
        var limit = Math.Clamp(take ?? 50, 1, 100);
        var items = await dbContext.AgentActivities.AsNoTracking()
            .Where(activity => activity.OrganizationId == tenant.OrganizationId)
            .OrderByDescending(activity => activity.OccurredAtUtc)
            .ThenByDescending(activity => activity.Sequence)
            .Take(limit)
            .Select(activity => new AgentActivityResponse(
                activity.Id,
                activity.AgentId,
                activity.DriverId,
                activity.VehicleId,
                activity.MissionId,
                activity.Sequence,
                activity.ObservedState.ToString(),
                activity.Policy,
                activity.Action.ToString(),
                activity.ResultCode,
                activity.ResultMessage,
                activity.OccurredAtUtc))
            .ToListAsync(cancellationToken);
        return Results.Ok(items);
    }

    private static async Task<IResult> RecordAsync(
        RecordAgentActivityRequest request,
        HttpContext context,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor tenants,
        IAuditService audit,
        IOperationsRealtimeNotifier notifier,
        CancellationToken cancellationToken)
    {
        var tenant = tenants.GetRequiredTenant(context.User);
        var existing = await dbContext.AgentActivities.AsNoTracking()
            .FirstOrDefaultAsync(activity => activity.OrganizationId == tenant.OrganizationId && activity.AgentId == request.AgentId && activity.Sequence == request.Sequence, cancellationToken);
        if (existing is not null) return Results.Ok(ToResponse(existing));

        var identity = new VirtualDriverIdentity(request.AgentId, tenant.OrganizationId, request.DriverId, request.VehicleId, request.MissionId, request.StopId);
        AgentActivity activity;
        try
        {
            activity = new(identity, request.Sequence, request.ObservedState, request.Policy, request.Action, request.ResultCode, request.ResultMessage, request.OccurredAtUtc);
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["activity"] = [exception.Message] });
        }

        var resourcesBelongToTenant = await dbContext.Drivers.AnyAsync(driver => driver.OrganizationId == tenant.OrganizationId && driver.Id == request.DriverId, cancellationToken)
            && await dbContext.Vehicles.AnyAsync(vehicle => vehicle.OrganizationId == tenant.OrganizationId && vehicle.Id == request.VehicleId, cancellationToken)
            && await dbContext.Missions.AnyAsync(mission => mission.OrganizationId == tenant.OrganizationId && mission.Id == request.MissionId, cancellationToken);
        if (!resourcesBelongToTenant) return Results.NotFound();

        dbContext.AgentActivities.Add(activity);
        await dbContext.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync(tenant.OrganizationId, tenant.UserId, "demo.agent_action", "virtual-driver-agent", activity.AgentId.ToString(), new { activity.Policy, activity.Action, activity.ResultCode, activity.Sequence }, cancellationToken);
        await notifier.NotifyQueueChangedAsync(tenant.OrganizationId, "demo.agent_activity_recorded", cancellationToken);
        return Results.Created($"/api/v1/demo/agent-activities/{activity.Id}", ToResponse(activity));
    }

    private static AgentActivityResponse ToResponse(AgentActivity activity) => new(
        activity.Id, activity.AgentId, activity.DriverId, activity.VehicleId, activity.MissionId, activity.Sequence,
        activity.ObservedState.ToString(), activity.Policy, activity.Action.ToString(), activity.ResultCode, activity.ResultMessage, activity.OccurredAtUtc);
}

public sealed record RecordAgentActivityRequest(
    Guid AgentId,
    Guid DriverId,
    Guid VehicleId,
    Guid MissionId,
    Guid StopId,
    long Sequence,
    VirtualDriverState ObservedState,
    string Policy,
    VirtualDriverAction Action,
    string ResultCode,
    string ResultMessage,
    DateTimeOffset OccurredAtUtc);

public sealed record AgentActivityResponse(
    Guid Id,
    Guid AgentId,
    Guid DriverId,
    Guid VehicleId,
    Guid MissionId,
    long Sequence,
    string ObservedState,
    string Policy,
    string Action,
    string ResultCode,
    string ResultMessage,
    DateTimeOffset OccurredAtUtc);
