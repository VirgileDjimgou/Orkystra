using FleetOps.Core.Common;

namespace FleetOps.Core.Modules.Tracking;

public enum TrackingQualityStatus { Fresh, Delayed, Inaccurate, Invalid, Silent }
public enum TrackingGeofenceShape { Circle, Polygon }
public enum TrackingGeofenceTransition { Entered, Exited }

public sealed class TrackingTrip : TenantEntity
{
    private TrackingTrip() { }

    public TrackingTrip(Guid organizationId, Guid vehicleId, DateTimeOffset startedAtUtc, DateTimeOffset endedAtUtc, double distanceKm, int stopCount, int pointCount, string algorithmVersion, DateTimeOffset calculatedAtUtc)
    {
        if (organizationId == Guid.Empty || vehicleId == Guid.Empty) throw new ArgumentException("Organization and vehicle are required.");
        if (endedAtUtc < startedAtUtc) throw new ArgumentException("Trip end must not precede its start.");
        OrganizationId = organizationId; VehicleId = vehicleId; StartedAtUtc = startedAtUtc.ToUniversalTime(); EndedAtUtc = endedAtUtc.ToUniversalTime();
        DistanceKm = Math.Max(0, distanceKm); StopCount = Math.Max(0, stopCount); PointCount = Math.Max(1, pointCount);
        AlgorithmVersion = string.IsNullOrWhiteSpace(algorithmVersion) ? throw new ArgumentException("Algorithm version is required.") : algorithmVersion.Trim();
        CalculatedAtUtc = calculatedAtUtc.ToUniversalTime();
    }

    public Guid VehicleId { get; private set; }
    public DateTimeOffset StartedAtUtc { get; private set; }
    public DateTimeOffset EndedAtUtc { get; private set; }
    public double DistanceKm { get; private set; }
    public int StopCount { get; private set; }
    public int PointCount { get; private set; }
    public string AlgorithmVersion { get; private set; } = string.Empty;
    public DateTimeOffset CalculatedAtUtc { get; private set; }
}

public sealed class TrackingGeofence : TenantEntity
{
    private TrackingGeofence() { }

    public TrackingGeofence(Guid organizationId, string name, TrackingGeofenceShape shape, double? centerLatitude, double? centerLongitude, double? radiusMeters, string polygonJson, DateTimeOffset createdAtUtc)
    {
        if (organizationId == Guid.Empty) throw new ArgumentException("Organization is required.");
        OrganizationId = organizationId;
        Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Name is required.") : name.Trim();
        Shape = shape; CenterLatitude = centerLatitude; CenterLongitude = centerLongitude; RadiusMeters = radiusMeters;
        PolygonJson = polygonJson?.Trim() ?? "[]"; CreatedAtUtc = createdAtUtc.ToUniversalTime();
    }

    public string Name { get; private set; } = string.Empty;
    public TrackingGeofenceShape Shape { get; private set; }
    public double? CenterLatitude { get; private set; }
    public double? CenterLongitude { get; private set; }
    public double? RadiusMeters { get; private set; }
    public string PolygonJson { get; private set; } = "[]";
}

public sealed class TrackingGeofenceEvent : TenantEntity
{
    private TrackingGeofenceEvent() { }

    public TrackingGeofenceEvent(Guid organizationId, Guid geofenceId, Guid vehicleId, string telemetryEventId, TrackingGeofenceTransition transition, DateTimeOffset occurredAtUtc)
    {
        if (organizationId == Guid.Empty || geofenceId == Guid.Empty || vehicleId == Guid.Empty) throw new ArgumentException("Organization, geofence and vehicle are required.");
        OrganizationId = organizationId; GeofenceId = geofenceId; VehicleId = vehicleId;
        TelemetryEventId = string.IsNullOrWhiteSpace(telemetryEventId) ? throw new ArgumentException("Telemetry event is required.") : telemetryEventId.Trim();
        Transition = transition; OccurredAtUtc = occurredAtUtc.ToUniversalTime();
    }

    public Guid GeofenceId { get; private set; }
    public Guid VehicleId { get; private set; }
    public string TelemetryEventId { get; private set; } = string.Empty;
    public TrackingGeofenceTransition Transition { get; private set; }
    public DateTimeOffset OccurredAtUtc { get; private set; }
}

public static class TrackingGeometry
{
    public static double DistanceKm(double latitude1, double longitude1, double latitude2, double longitude2)
    {
        const double earthRadiusKm = 6371.0088;
        var lat1 = DegreesToRadians(latitude1); var lat2 = DegreesToRadians(latitude2);
        var deltaLat = DegreesToRadians(latitude2 - latitude1); var deltaLon = DegreesToRadians(longitude2 - longitude1);
        var a = Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2) + Math.Cos(lat1) * Math.Cos(lat2) * Math.Sin(deltaLon / 2) * Math.Sin(deltaLon / 2);
        return earthRadiusKm * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    public static bool ContainsCircle(double latitude, double longitude, double centerLatitude, double centerLongitude, double radiusMeters) =>
        DistanceKm(latitude, longitude, centerLatitude, centerLongitude) * 1000 <= radiusMeters;

    public static bool ContainsPolygon(double latitude, double longitude, IReadOnlyList<(double Latitude, double Longitude)> points)
    {
        var inside = false;
        for (var i = 0; i < points.Count; i++)
        {
            var j = i == 0 ? points.Count - 1 : i - 1;
            var intersects = (points[i].Latitude > latitude) != (points[j].Latitude > latitude)
                && longitude < (points[j].Longitude - points[i].Longitude) * (latitude - points[i].Latitude)
                    / (points[j].Latitude - points[i].Latitude) + points[i].Longitude;
            if (intersects) inside = !inside;
        }
        return inside;
    }

    private static double DegreesToRadians(double value) => value * Math.PI / 180d;
}
