using System.Text.Json;
using FleetOps.Api.Security;
using FleetOps.Core.Modules.Identity;
using FleetOps.Core.Modules.Tracking;
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
        tracking.MapGet("/diagnostics", GetDiagnosticsAsync);
        tracking.MapGet("/trips", GetTripsAsync);
        tracking.MapGet("/geofences", GetGeofencesAsync);
        tracking.MapGet("/geofence-events", GetGeofenceEventsAsync);
        tracking.MapPost("/trips/recalculate", RecalculateTripsAsync)
            .RequireAuthorization(new AuthorizeAttribute { Roles = SystemRoles.Admin });
        tracking.MapPost("/geofences", CreateGeofenceAsync)
            .RequireAuthorization(new AuthorizeAttribute { Roles = SystemRoles.Admin });

        var internalTracking = app.MapGroup("/api/internal/v1/tracking");
        internalTracking.MapPost("/events", IngestInternalAsync);
        internalTracking.MapGet("/scenarios/{organizationSlug}", GetScenarioAsync);
        internalTracking.MapPost("/scenarios/{organizationSlug}/reset", ResetScenarioAsync);

        // Compatibility aliases kept for earlier sprint demos.
        app.MapGet("/api/tracking/latest", GetLegacyLatestAsync)
            .AddEndpointFilter(AddDeprecationHeadersAsync)
            .RequireAuthorization(new AuthorizeAttribute { Roles = ReaderRoles });
        app.MapPost("/api/simulation/telemetry", IngestLegacySimulationAsync)
            .AddEndpointFilter(AddDeprecationHeadersAsync);

        return app;
    }

    private static async ValueTask<object?> AddDeprecationHeadersAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        context.HttpContext.Response.Headers.Append("Deprecation", "true");
        context.HttpContext.Response.Headers.Append("Link", "</api/v1/tracking>; rel=successor-version");
        return await next(context);
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
                current.HeadingDegrees,
                current.SequenceNumber,
                current.AccuracyMeters,
                current.Source,
                current.QualityScore,
                PositionStatus(current, DateTimeOffset.UtcNow).Status,
                PositionStatus(current, DateTimeOffset.UtcNow).Reason)
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
                x.HeadingDegrees,
                x.SequenceNumber,
                x.AccuracyMeters,
                x.Source,
                x.QualityScore,
                x.AnomalyFlags))
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

    private static async Task<IResult> GetDiagnosticsAsync(HttpContext httpContext, FleetOpsDbContext dbContext, ICurrentTenantAccessor currentTenantAccessor, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        var rows = await (
            from vehicle in dbContext.Vehicles
            join position in dbContext.CurrentVehiclePositions on vehicle.Id equals position.VehicleId into positions
            from position in positions.DefaultIfEmpty()
            join assignment in dbContext.DeviceAssignments.Where(x => x.UnassignedAtUtc == null) on vehicle.Id equals assignment.VehicleId into assignments
            from assignment in assignments.DefaultIfEmpty()
            where vehicle.OrganizationId == tenant.OrganizationId && vehicle.IsActive
            select new { vehicle, position, assignment }).ToListAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        return Results.Ok(rows.Select(x =>
        {
            var status = x.position is null ? ("Silent", "No accepted telemetry has been received.") : PositionStatus(x.position, now);
            return new TrackingDiagnosticResponse(x.vehicle.Id, x.vehicle.RegistrationNumber, x.vehicle.DisplayName, null, x.position?.DeviceId ?? x.assignment?.DeviceId.ToString() ?? "unassigned", x.position?.IngestedAtUtc, status.Item1, status.Item2, x.position?.QualityScore ?? 0, x.position?.AccuracyMeters, x.position?.Source ?? "unknown", x.position?.SequenceNumber);
        }));
    }

    private static async Task<IResult> GetTripsAsync(Guid? vehicleId, HttpContext httpContext, FleetOpsDbContext dbContext, ICurrentTenantAccessor currentTenantAccessor, CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        var query = dbContext.TrackingTrips.Where(x => x.OrganizationId == tenant.OrganizationId);
        if (vehicleId.HasValue) query = query.Where(x => x.VehicleId == vehicleId.Value);
        var result = await query.OrderByDescending(x => x.StartedAtUtc).Take(100).Select(x => new TrackingTripResponse(x.Id, x.VehicleId, x.StartedAtUtc, x.EndedAtUtc, x.DistanceKm, x.StopCount, x.PointCount, x.AlgorithmVersion)).ToListAsync(cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> RecalculateTripsAsync(RecalculateTripsRequest request, HttpContext httpContext, ICurrentTenantAccessor currentTenantAccessor, TrackingDerivationService derivationService, CancellationToken cancellationToken)
    {
        try { return Results.Ok(await derivationService.RecalculateTripsAsync(currentTenantAccessor.GetRequiredTenant(httpContext.User).OrganizationId, request, cancellationToken)); }
        catch (TrackingValidationException ex) { return Results.ValidationProblem(new Dictionary<string, string[]> { [ex.Field] = [ex.Message] }); }
    }

    private static async Task<IResult> GetGeofencesAsync(HttpContext httpContext, FleetOpsDbContext dbContext, ICurrentTenantAccessor currentTenantAccessor, CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        var fences = await dbContext.TrackingGeofences.Where(x => x.OrganizationId == tenant.OrganizationId).OrderBy(x => x.Name).ToListAsync(cancellationToken);
        return Results.Ok(fences.Select(ToGeofenceResponse));
    }

    private static async Task<IResult> CreateGeofenceAsync(CreateTrackingGeofenceRequest request, HttpContext httpContext, FleetOpsDbContext dbContext, ICurrentTenantAccessor currentTenantAccessor, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 100) return Results.ValidationProblem(new Dictionary<string, string[]> { ["name"] = ["Name must contain up to 100 characters."] });
        if (!Enum.TryParse<TrackingGeofenceShape>(request.Shape, true, out var shape)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["shape"] = ["Shape must be Circle or Polygon."] });
        var polygon = request.Polygon ?? [];
        if (shape == TrackingGeofenceShape.Circle && (!IsCoordinate(request.CenterLatitude, request.CenterLongitude) || request.RadiusMeters is < 10 or > 50000)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["geofence"] = ["A circle requires a valid center and radius between 10 and 50000 metres."] });
        if (shape == TrackingGeofenceShape.Polygon && (polygon.Count is < 3 or > 50 || polygon.Any(x => !IsCoordinate(x.Latitude, x.Longitude)))) return Results.ValidationProblem(new Dictionary<string, string[]> { ["polygon"] = ["A polygon requires 3 to 50 valid coordinates."] });
        if (await dbContext.TrackingGeofences.AnyAsync(x => x.OrganizationId == tenant.OrganizationId && x.Name == request.Name.Trim(), cancellationToken)) return Results.Conflict(new { message = "A geofence with this name already exists." });
        var fence = new TrackingGeofence(tenant.OrganizationId, request.Name, shape, request.CenterLatitude, request.CenterLongitude, request.RadiusMeters, JsonSerializer.Serialize(polygon), timeProvider.GetUtcNow());
        dbContext.TrackingGeofences.Add(fence); await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/v1/tracking/geofences/{fence.Id}", ToGeofenceResponse(fence));
    }

    private static async Task<IResult> GetGeofenceEventsAsync(HttpContext httpContext, FleetOpsDbContext dbContext, ICurrentTenantAccessor currentTenantAccessor, CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        var events = await (from item in dbContext.TrackingGeofenceEvents join fence in dbContext.TrackingGeofences on item.GeofenceId equals fence.Id join vehicle in dbContext.Vehicles on item.VehicleId equals vehicle.Id where item.OrganizationId == tenant.OrganizationId && fence.OrganizationId == tenant.OrganizationId && vehicle.OrganizationId == tenant.OrganizationId orderby item.OccurredAtUtc descending select new TrackingGeofenceEventResponse(item.Id, item.GeofenceId, fence.Name, item.VehicleId, vehicle.RegistrationNumber, item.Transition.ToString(), item.OccurredAtUtc, item.TelemetryEventId)).Take(100).ToListAsync(cancellationToken);
        return Results.Ok(events);
    }

    private static TrackingGeofenceResponse ToGeofenceResponse(TrackingGeofence fence) => new(fence.Id, fence.Name, fence.Shape.ToString(), fence.CenterLatitude, fence.CenterLongitude, fence.RadiusMeters, JsonSerializer.Deserialize<List<TrackingCoordinateRequest>>(fence.PolygonJson) ?? []);
    private static bool IsCoordinate(double? latitude, double? longitude) => latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180;
    private static bool IsCoordinate(double latitude, double longitude) => IsCoordinate((double?)latitude, longitude);
    private static (string Status, string Reason) PositionStatus(CurrentVehiclePosition position, DateTimeOffset now)
    {
        if (position.QualityScore < 50 || position.AnomalyFlags.Contains("implausible-jump", StringComparison.Ordinal) || position.AnomalyFlags.Contains("clock-skew", StringComparison.Ordinal)) return ("Invalid", string.IsNullOrEmpty(position.AnomalyFlags) ? "Telemetry failed quality checks." : position.AnomalyFlags);
        if (position.AccuracyMeters is > 100) return ("Inaccurate", "GPS accuracy is above 100 metres.");
        if (now - position.IngestedAtUtc > TimeSpan.FromMinutes(10)) return ("Silent", "No recent communication was received.");
        if (now - position.IngestedAtUtc > TimeSpan.FromMinutes(2)) return ("Delayed", "Last telemetry is older than two minutes.");
        return ("Fresh", "Position is reliable.");
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
