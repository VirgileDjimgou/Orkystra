using System.Diagnostics;
using FleetOps.Core.Modules.Tracking;
using FleetOps.Core.Observability;
using FleetOps.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

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
    TrackingQualityAnalyzer qualityAnalyzer,
    TrackingDerivationService derivationService,
    TimeProvider timeProvider)
{
    public async Task<TelemetryIngestionResponse> IngestAsync(
        IngestTelemetryRequest request,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            if (await dbContext.TelemetryPoints.AnyAsync(
                    x => x.OrganizationId == request.OrganizationId && x.EventId == request.EventId,
                    cancellationToken))
            {
                return Duplicate(request, stopwatch);
            }

            var vehicle = await dbContext.Vehicles
                .Where(x => x.OrganizationId == request.OrganizationId && x.Id == request.VehicleId && x.IsActive)
                .Select(x => new { x.RegistrationNumber, x.DisplayName })
                .FirstOrDefaultAsync(cancellationToken);
            if (vehicle is null)
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

            var currentPosition = await dbContext.CurrentVehiclePositions
                .FirstOrDefaultAsync(
                    x => x.OrganizationId == request.OrganizationId && x.VehicleId == request.VehicleId,
                    cancellationToken);
            var assessment = qualityAnalyzer.Assess(currentPosition, request);
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
                timeProvider.GetUtcNow(), request.SequenceNumber, request.AccuracyMeters, request.Source, assessment.Score, assessment.Flags);

            dbContext.TelemetryPoints.Add(point);

            var currentUpdated = false;
            var outOfOrder = false;
            var rejected = false;
            if (currentPosition is null && assessment.IsReliable)
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
                    point.HeadingDegrees, point.IngestedAtUtc, point.SequenceNumber, point.AccuracyMeters, point.Source, point.QualityScore, point.AnomalyFlags));
                currentUpdated = true;
            }
            else if (currentPosition is not null && assessment.IsReliable && point.RecordedAtUtc > currentPosition.RecordedAtUtc)
            {
                currentPosition.UpdateFrom(point);
                currentUpdated = true;
            }
            else
            {
                outOfOrder = currentPosition is not null && point.RecordedAtUtc <= currentPosition.RecordedAtUtc;
                rejected = !assessment.IsReliable;
            }

            await derivationService.ProcessGeofencesAsync(point, cancellationToken);
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // A concurrent request can win the unique (OrganizationId, EventId) race.
                // Ingestion is idempotent: confirm the stored event and answer duplicate.
                if (await dbContext.TelemetryPoints.AsNoTracking().AnyAsync(
                        x => x.OrganizationId == request.OrganizationId && x.EventId == request.EventId,
                        cancellationToken))
                {
                    return Duplicate(request, stopwatch);
                }

                throw;
            }

            metricsStore.RecordAccepted(request.OrganizationId, outOfOrder);
            if (rejected)
            {
                metricsStore.RecordRejected(request.OrganizationId);
            }

            if (currentUpdated)
            {
                await BroadcastAsync(request, vehicle.RegistrationNumber, vehicle.DisplayName, point, cancellationToken);
            }

            var result = outOfOrder ? "out_of_order" : rejected ? "rejected" : "accepted";
            return Complete(stopwatch, result,
                new TelemetryIngestionResponse("accepted", false, outOfOrder, currentUpdated, 0));
        }
        catch (TrackingValidationException)
        {
            Record(stopwatch, "rejected");
            throw;
        }
        catch (ArgumentException)
        {
            Record(stopwatch, "rejected");
            throw;
        }
        catch
        {
            Record(stopwatch, "error");
            throw;
        }
    }

    private async Task BroadcastAsync(
        IngestTelemetryRequest request,
        string registrationNumber,
        string displayName,
        TelemetryPoint point,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await hubContext.Clients.Group($"organization:{request.OrganizationId}")
                .SendAsync(
                    "trackingPositionChanged",
                    TrackingPositionMapper.FromTelemetry(
                        point,
                        registrationNumber,
                        displayName,
                        timeProvider.GetUtcNow()),
                    cancellationToken);
            FleetOpsMetrics.TrackingBroadcastEvents.Add(1, new KeyValuePair<string, object?>("outcome", "sent"));
        }
        catch
        {
            FleetOpsMetrics.TrackingBroadcastEvents.Add(1, new KeyValuePair<string, object?>("outcome", "failed"));
            throw;
        }
        finally
        {
            FleetOpsMetrics.TrackingBroadcastDuration.Record(
                stopwatch.Elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>("outcome", "completed"));
        }
    }

    private TelemetryIngestionResponse Duplicate(IngestTelemetryRequest request, Stopwatch stopwatch)
    {
        metricsStore.RecordDuplicate(request.OrganizationId);
        Record(stopwatch, "duplicate");
        return new TelemetryIngestionResponse("duplicate", true, false, false, 0);
    }

    private static TelemetryIngestionResponse Complete(
        Stopwatch stopwatch,
        string result,
        TelemetryIngestionResponse response)
    {
        Record(stopwatch, result);
        return response;
    }

    private static void Record(Stopwatch stopwatch, string result)
    {
        var tag = new KeyValuePair<string, object?>("result", result);
        FleetOpsMetrics.TrackingIngestEvents.Add(1, tag);
        FleetOpsMetrics.TrackingIngestDuration.Record(stopwatch.Elapsed.TotalMilliseconds, tag);
    }
}
