using FleetOps.Core.Modules.Tracking;
using FleetOps.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FleetOps.Api.Tracking;

public sealed class TrackingValidationException(string field, string message) : Exception(message)
{
    public string Field { get; } = field;
}

public sealed class TrackingOptions
{
    public const string SectionName = "Tracking";

    public int RetentionDays { get; init; } = 7;
    public int MaxHistoryPageSize { get; init; } = 100;
}

public sealed class TrackingIngestionService(
    FleetOpsDbContext dbContext,
    IHubContext<TrackingHub> hubContext,
    TrackingMetricsStore metricsStore,
    TimeProvider timeProvider,
    IOptions<TrackingOptions> options)
{
    public async Task<TelemetryIngestionResponse> IngestAsync(
        IngestTelemetryRequest request,
        CancellationToken cancellationToken)
    {
        if (await dbContext.TelemetryPoints.AnyAsync(
                x => x.OrganizationId == request.OrganizationId && x.EventId == request.EventId,
                cancellationToken))
        {
            metricsStore.RecordDuplicate(request.OrganizationId);
            return new TelemetryIngestionResponse("duplicate", true, false, false, 0);
        }

        var vehicleExists = await dbContext.Vehicles.AnyAsync(
            x => x.OrganizationId == request.OrganizationId && x.Id == request.VehicleId && x.IsActive,
            cancellationToken);
        if (!vehicleExists)
        {
            throw new TrackingValidationException("vehicleId", "Vehicle does not exist or is inactive in this organization.");
        }

        var deviceAssigned = await (
            from device in dbContext.GpsDevices
            join assignment in dbContext.DeviceAssignments on device.Id equals assignment.DeviceId
            where device.OrganizationId == request.OrganizationId
                && device.IsActive
                && device.SerialNumber == request.DeviceId
                && assignment.OrganizationId == request.OrganizationId
                && assignment.VehicleId == request.VehicleId
                && assignment.UnassignedAtUtc == null
            select device.Id
        ).AnyAsync(cancellationToken);
        if (!deviceAssigned)
        {
            throw new TrackingValidationException("deviceId", "Device is not actively assigned to the target vehicle.");
        }

        var point = new TelemetryPoint(
            request.OrganizationId,
            request.VehicleId,
            request.DeviceId,
            request.EventId,
            request.RecordedAtUtc,
            request.Latitude,
            request.Longitude,
            request.SpeedKph,
            request.HeadingDegrees,
            timeProvider.GetUtcNow());

        dbContext.TelemetryPoints.Add(point);

        var currentPosition = await dbContext.CurrentVehiclePositions
            .FirstOrDefaultAsync(
                x => x.OrganizationId == request.OrganizationId && x.VehicleId == request.VehicleId,
                cancellationToken);

        var currentUpdated = false;
        var outOfOrder = false;
        if (currentPosition is null)
        {
            dbContext.CurrentVehiclePositions.Add(new CurrentVehiclePosition(
                point.OrganizationId,
                point.VehicleId,
                point.DeviceId,
                point.EventId,
                point.RecordedAtUtc,
                point.Latitude,
                point.Longitude,
                point.SpeedKph,
                point.HeadingDegrees));
            currentUpdated = true;
        }
        else if (point.RecordedAtUtc > currentPosition.RecordedAtUtc)
        {
            currentPosition.UpdateFrom(point);
            currentUpdated = true;
        }
        else
        {
            outOfOrder = true;
        }

        var retentionDeletedCount = await ApplyRetentionAsync(request.OrganizationId, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        metricsStore.RecordAccepted(request.OrganizationId, outOfOrder);

        if (currentUpdated)
        {
            var vehicle = await dbContext.Vehicles
                .Where(x => x.OrganizationId == request.OrganizationId && x.Id == request.VehicleId)
                .Select(x => new { x.RegistrationNumber, x.DisplayName })
                .FirstAsync(cancellationToken);

            await hubContext.Clients.Group($"organization:{request.OrganizationId}")
                .SendAsync(
                    "trackingPositionChanged",
                    new TrackingPositionResponse(
                        point.VehicleId,
                        vehicle.RegistrationNumber,
                        vehicle.DisplayName,
                        point.DeviceId,
                        point.RecordedAtUtc,
                        point.Latitude,
                        point.Longitude,
                        point.SpeedKph,
                        point.HeadingDegrees),
                    cancellationToken);
        }

        return new TelemetryIngestionResponse("accepted", false, outOfOrder, currentUpdated, retentionDeletedCount);
    }

    private async Task<int> ApplyRetentionAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var retentionDays = Math.Max(1, options.Value.RetentionDays);
        var cutoffUtc = timeProvider.GetUtcNow().AddDays(-retentionDays);
        var toDelete = await dbContext.TelemetryPoints
            .Where(x => x.OrganizationId == organizationId && x.RecordedAtUtc < cutoffUtc)
            .ToListAsync(cancellationToken);

        if (toDelete.Count == 0)
        {
            return 0;
        }

        dbContext.TelemetryPoints.RemoveRange(toDelete);
        return toDelete.Count;
    }
}
