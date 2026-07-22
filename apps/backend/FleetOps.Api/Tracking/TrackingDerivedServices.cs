using System.Text.Json;
using FleetOps.Core.Modules.Tracking;
using FleetOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FleetOps.Api.Tracking;

public sealed record TrackingAssessment(int Score, string Flags)
{
    public bool IsReliable => Score >= 50 && !Flags.Contains("implausible-jump", StringComparison.Ordinal) && !Flags.Contains("clock-skew", StringComparison.Ordinal) && !Flags.Contains("sequence-regression", StringComparison.Ordinal);
}

public sealed class TrackingQualityAnalyzer(TimeProvider timeProvider)
{
    public TrackingAssessment Assess(CurrentVehiclePosition? current, IngestTelemetryRequest request)
    {
        var flags = new List<string>(); var score = 100; var now = timeProvider.GetUtcNow();
        if (request.AccuracyMeters is > 100) { flags.Add("low-accuracy"); score -= 35; }
        if (request.RecordedAtUtc > now.AddMinutes(5)) { flags.Add("clock-skew"); score = Math.Min(score, 20); }
        if (request.RecordedAtUtc < now.AddHours(-24)) { flags.Add("stale-source"); score -= 20; }
        if (current is not null)
        {
            var elapsedHours = (request.RecordedAtUtc - current.RecordedAtUtc).TotalHours;
            if (elapsedHours > 10d / 60d) { flags.Add("communication-gap"); score -= 10; }
            if (request.SequenceNumber.HasValue && current.SequenceNumber.HasValue && request.SequenceNumber <= current.SequenceNumber) { flags.Add("sequence-regression"); score = Math.Min(score, 25); }
            if (elapsedHours > 0)
            {
                var impliedSpeed = TrackingGeometry.DistanceKm(current.Latitude, current.Longitude, request.Latitude, request.Longitude) / elapsedHours;
                if (impliedSpeed > 200) { flags.Add("implausible-jump"); score = Math.Min(score, 20); }
            }
        }
        return new TrackingAssessment(Math.Clamp(score, 0, 100), string.Join(',', flags));
    }
}

public sealed class TrackingDerivationService(FleetOpsDbContext dbContext, TimeProvider timeProvider)
{
    public const string TripAlgorithmVersion = "tracking-trip-v1";

    public async Task ProcessGeofencesAsync(TelemetryPoint point, CancellationToken cancellationToken)
    {
        if (point.QualityScore < 50 || point.AnomalyFlags.Contains("implausible-jump", StringComparison.Ordinal)) return;
        var fences = await dbContext.TrackingGeofences.Where(x => x.OrganizationId == point.OrganizationId).ToListAsync(cancellationToken);
        foreach (var fence in fences)
        {
            var inside = IsInside(fence, point.Latitude, point.Longitude);
            var previous = await dbContext.TrackingGeofenceEvents.Where(x => x.OrganizationId == point.OrganizationId && x.GeofenceId == fence.Id && x.VehicleId == point.VehicleId).OrderByDescending(x => x.OccurredAtUtc).FirstOrDefaultAsync(cancellationToken);
            var previouslyInside = previous?.Transition == TrackingGeofenceTransition.Entered;
            if (inside == previouslyInside) continue;
            var transition = inside ? TrackingGeofenceTransition.Entered : TrackingGeofenceTransition.Exited;
            var duplicate = await dbContext.TrackingGeofenceEvents.AnyAsync(x => x.OrganizationId == point.OrganizationId && x.GeofenceId == fence.Id && x.VehicleId == point.VehicleId && x.TelemetryEventId == point.EventId && x.Transition == transition, cancellationToken);
            if (!duplicate) dbContext.TrackingGeofenceEvents.Add(new TrackingGeofenceEvent(point.OrganizationId, fence.Id, point.VehicleId, point.EventId, transition, point.RecordedAtUtc));
        }
    }

    public async Task<RecalculateTripsResponse> RecalculateTripsAsync(Guid organizationId, RecalculateTripsRequest request, CancellationToken cancellationToken)
    {
        var from = request.FromUtc.ToUniversalTime(); var to = request.ToUtc.ToUniversalTime();
        if (request.VehicleId == Guid.Empty || to <= from || to - from > TimeSpan.FromDays(7)) throw new TrackingValidationException("range", "Vehicle and a range of up to seven days are required.");
        var vehicleExists = await dbContext.Vehicles.AnyAsync(x => x.OrganizationId == organizationId && x.Id == request.VehicleId, cancellationToken);
        if (!vehicleExists) throw new TrackingValidationException("vehicleId", "Vehicle does not exist in this organization.");
        var existing = await dbContext.TrackingTrips.Where(x => x.OrganizationId == organizationId && x.VehicleId == request.VehicleId && x.StartedAtUtc < to && x.EndedAtUtc >= from).ToListAsync(cancellationToken);
        dbContext.TrackingTrips.RemoveRange(existing);
        var points = await dbContext.TelemetryPoints.Where(x => x.OrganizationId == organizationId && x.VehicleId == request.VehicleId && x.RecordedAtUtc >= from && x.RecordedAtUtc <= to).OrderBy(x => x.RecordedAtUtc).ToListAsync(cancellationToken);
        var created = 0; var segment = new List<TelemetryPoint>();
        foreach (var point in points.Where(x => x.QualityScore >= 50))
        {
            if (segment.Count > 0 && point.RecordedAtUtc - segment[^1].RecordedAtUtc > TimeSpan.FromMinutes(10)) { created += AddTrip(organizationId, request.VehicleId, segment); segment = []; }
            segment.Add(point);
        }
        created += AddTrip(organizationId, request.VehicleId, segment);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new RecalculateTripsResponse(existing.Count, created, TripAlgorithmVersion);
    }

    private int AddTrip(Guid organizationId, Guid vehicleId, List<TelemetryPoint> points)
    {
        if (points.Count < 2) return 0;
        var distance = 0d; var stops = 0;
        for (var i = 1; i < points.Count; i++)
        {
            distance += TrackingGeometry.DistanceKm(points[i - 1].Latitude, points[i - 1].Longitude, points[i].Latitude, points[i].Longitude);
            if (points[i].SpeedKph < 3 && points[i - 1].SpeedKph < 3 && points[i].RecordedAtUtc - points[i - 1].RecordedAtUtc >= TimeSpan.FromMinutes(5)) stops++;
        }
        dbContext.TrackingTrips.Add(new TrackingTrip(organizationId, vehicleId, points[0].RecordedAtUtc, points[^1].RecordedAtUtc, distance, stops, points.Count, TripAlgorithmVersion, timeProvider.GetUtcNow()));
        return 1;
    }

    public static bool IsInside(TrackingGeofence fence, double latitude, double longitude)
    {
        if (fence.Shape == TrackingGeofenceShape.Circle) return fence.CenterLatitude.HasValue && fence.CenterLongitude.HasValue && fence.RadiusMeters.HasValue && TrackingGeometry.ContainsCircle(latitude, longitude, fence.CenterLatitude.Value, fence.CenterLongitude.Value, fence.RadiusMeters.Value);
        var polygon = JsonSerializer.Deserialize<List<TrackingCoordinateRequest>>(fence.PolygonJson) ?? [];
        return polygon.Count >= 3 && TrackingGeometry.ContainsPolygon(latitude, longitude, polygon.Select(x => (x.Latitude, x.Longitude)).ToList());
    }
}
