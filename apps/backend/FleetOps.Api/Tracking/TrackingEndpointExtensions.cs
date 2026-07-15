using FleetOps.Api.Security;
using FleetOps.Core.Modules.Identity;
using FleetOps.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FleetOps.Api.Tracking;

public static class TrackingEndpointExtensions
{
    private const string ReaderRoles = SystemRoles.Admin + "," + SystemRoles.Operator;

    public static IEndpointRouteBuilder MapTrackingEndpoints(this IEndpointRouteBuilder app)
    {
        var tracking = app.MapGroup("/api/v1/tracking")
            .RequireAuthorization(new AuthorizeAttribute { Roles = ReaderRoles });
        tracking.MapGet("/positions", ListPositionsAsync);
        tracking.MapGet("/history", GetHistoryAsync);
        tracking.MapGet("/metrics", GetMetricsAsync);

        var internalTracking = app.MapGroup("/api/internal/v1/tracking");
        internalTracking.MapPost("/events", IngestInternalAsync);
        internalTracking.MapGet("/scenarios/{organizationSlug}", GetScenarioAsync);
        internalTracking.MapPost("/scenarios/{organizationSlug}/reset", ResetScenarioAsync);

        // Compatibility aliases kept for earlier sprint demos.
        app.MapGet("/api/tracking/latest", GetLegacyLatestAsync)
            .RequireAuthorization(new AuthorizeAttribute { Roles = ReaderRoles });
        app.MapPost("/api/simulation/telemetry", IngestLegacySimulationAsync);

        return app;
    }

    private static async Task<IResult> ListPositionsAsync(
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        var positions = await (
            from current in dbContext.CurrentVehiclePositions
            join vehicle in dbContext.Vehicles on current.VehicleId equals vehicle.Id
            where current.OrganizationId == tenant.OrganizationId
                && vehicle.OrganizationId == tenant.OrganizationId
            orderby vehicle.RegistrationNumber
            select new TrackingPositionResponse(
                current.VehicleId,
                vehicle.RegistrationNumber,
                vehicle.DisplayName,
                current.DeviceId,
                current.RecordedAtUtc,
                current.Latitude,
                current.Longitude,
                current.SpeedKph,
                current.HeadingDegrees)
        ).ToListAsync(cancellationToken);

        return Results.Ok(positions);
    }

    private static async Task<IResult> GetHistoryAsync(
        Guid vehicleId,
        int page,
        int pageSize,
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        IOptions<TrackingOptions> options,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        if (page <= 0)
        {
            page = 1;
        }

        var maxPageSize = Math.Max(1, options.Value.MaxHistoryPageSize);
        if (pageSize <= 0)
        {
            pageSize = 20;
        }
        pageSize = Math.Min(pageSize, maxPageSize);

        var vehicleExists = await dbContext.Vehicles.AnyAsync(
            x => x.OrganizationId == tenant.OrganizationId && x.Id == vehicleId,
            cancellationToken);
        if (!vehicleExists)
        {
            return Results.NotFound();
        }

        var query = dbContext.TelemetryPoints
            .Where(x => x.OrganizationId == tenant.OrganizationId && x.VehicleId == vehicleId)
            .OrderByDescending(x => x.RecordedAtUtc);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new TrackingHistoryItemResponse(
                x.EventId,
                x.VehicleId,
                x.DeviceId,
                x.RecordedAtUtc,
                x.IngestedAtUtc,
                x.Latitude,
                x.Longitude,
                x.SpeedKph,
                x.HeadingDegrees))
            .ToListAsync(cancellationToken);

        return Results.Ok(new TrackingHistoryPageResponse(page, pageSize, totalCount, items));
    }

    private static async Task<IResult> GetMetricsAsync(
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        TrackingMetricsStore metricsStore,
        IOptions<TrackingOptions> options,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        var snapshot = metricsStore.GetSnapshot(tenant.OrganizationId);
        var currentVehicleCount = await dbContext.CurrentVehiclePositions.CountAsync(
            x => x.OrganizationId == tenant.OrganizationId,
            cancellationToken);
        var historyPointCount = await dbContext.TelemetryPoints.CountAsync(
            x => x.OrganizationId == tenant.OrganizationId,
            cancellationToken);

        return Results.Ok(new TrackingMetricsResponse(
            currentVehicleCount,
            historyPointCount,
            snapshot.Accepted,
            snapshot.Duplicate,
            snapshot.OutOfOrder,
            Math.Max(1, options.Value.RetentionDays)));
    }

    private static async Task<IResult> IngestInternalAsync(
        IngestTelemetryRequest request,
        IWebHostEnvironment environment,
        TrackingIngestionService ingestionService,
        CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
        {
            return Results.NotFound();
        }

        try
        {
            var response = await ingestionService.IngestAsync(request, cancellationToken);
            return Results.Accepted("/api/v1/tracking/positions", response);
        }
        catch (TrackingValidationException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [ex.Field] = [ex.Message]
            });
        }
        catch (ArgumentException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [ex.ParamName ?? "body"] = [ex.Message]
            });
        }
    }

    private static async Task<IResult> GetScenarioAsync(
        string organizationSlug,
        IWebHostEnvironment environment,
        FleetOpsDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
        {
            return Results.NotFound();
        }

        var organization = await dbContext.Organizations
            .FirstOrDefaultAsync(x => x.Slug == organizationSlug, cancellationToken);
        if (organization is null)
        {
            return Results.NotFound();
        }

        var vehicles = await (
            from vehicle in dbContext.Vehicles
            join assignment in dbContext.DeviceAssignments on vehicle.Id equals assignment.VehicleId
            join device in dbContext.GpsDevices on assignment.DeviceId equals device.Id
            where vehicle.OrganizationId == organization.Id
                && assignment.OrganizationId == organization.Id
                && assignment.UnassignedAtUtc == null
                && vehicle.IsActive
                && device.IsActive
            orderby vehicle.RegistrationNumber
            select new TrackingScenarioVehicleResponse(
                vehicle.Id,
                vehicle.RegistrationNumber,
                vehicle.DisplayName,
                device.SerialNumber)
        ).Take(3).ToListAsync(cancellationToken);

        if (vehicles.Count < 3)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["scenario"] = ["The organization does not have three active vehicle/device assignments."]
            });
        }

        return Results.Ok(new TrackingScenarioResponse(
            organization.Id,
            organization.Name,
            organization.Slug,
            vehicles));
    }

    private static async Task<IResult> ResetScenarioAsync(
        string organizationSlug,
        IWebHostEnvironment environment,
        FleetOpsDbContext dbContext,
        TrackingMetricsStore metricsStore,
        CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
        {
            return Results.NotFound();
        }

        var organization = await dbContext.Organizations
            .FirstOrDefaultAsync(x => x.Slug == organizationSlug, cancellationToken);
        if (organization is null)
        {
            return Results.NotFound();
        }

        var history = await dbContext.TelemetryPoints
            .Where(x => x.OrganizationId == organization.Id)
            .ToListAsync(cancellationToken);
        var currentPositions = await dbContext.CurrentVehiclePositions
            .Where(x => x.OrganizationId == organization.Id)
            .ToListAsync(cancellationToken);

        dbContext.TelemetryPoints.RemoveRange(history);
        dbContext.CurrentVehiclePositions.RemoveRange(currentPositions);
        await dbContext.SaveChangesAsync(cancellationToken);
        metricsStore.Reset(organization.Id);

        return Results.Ok(new TrackingScenarioResetResponse(history.Count, currentPositions.Count));
    }

    private static async Task<IResult> GetLegacyLatestAsync(
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        var positions = await dbContext.CurrentVehiclePositions
            .Where(x => x.OrganizationId == tenant.OrganizationId)
            .OrderBy(x => x.VehicleId)
            .Select(x => new TelemetryContract(
                x.OrganizationId,
                x.VehicleId,
                x.DeviceId,
                x.RecordedAtUtc,
                x.Latitude,
                x.Longitude,
                x.SpeedKph,
                x.HeadingDegrees))
            .ToListAsync(cancellationToken);

        return Results.Ok(positions);
    }

    private static Task<IResult> IngestLegacySimulationAsync(
        TelemetryContract request,
        IWebHostEnvironment environment,
        TrackingIngestionService ingestionService,
        CancellationToken cancellationToken)
    {
        var eventId = $"{request.VehicleId:N}:{request.DeviceId}:{request.RecordedAtUtc.ToUnixTimeMilliseconds()}";
        return IngestInternalAsync(
            new IngestTelemetryRequest(
                request.OrganizationId,
                request.VehicleId,
                request.DeviceId,
                eventId,
                request.RecordedAtUtc,
                request.Latitude,
                request.Longitude,
                request.SpeedKph,
                request.HeadingDegrees),
            environment,
            ingestionService,
            cancellationToken);
    }
}
