using System.Net.Http.Json;

var dryRun = args.Contains("--dry-run", StringComparer.OrdinalIgnoreCase);
var apiBaseUrl = Environment.GetEnvironmentVariable("FLEETOPS_API_URL") ?? "http://localhost:5080";
var organizationId = ParseGuid("SIM_ORGANIZATION_ID", "00000000-0000-0000-0000-000000000001");
var vehicleId = ParseGuid("SIM_VEHICLE_ID", "00000000-0000-0000-0000-000000000101");
var deviceId = Environment.GetEnvironmentVariable("SIM_DEVICE_ID") ?? "SIM-GPS-001";

using var http = new HttpClient { BaseAddress = new Uri(apiBaseUrl) };
Console.WriteLine($"FleetOps GPS simulator. API={apiBaseUrl}, dryRun={dryRun}");

var route = BuildRoute(48.4914, 9.2043, 0.018, 80);
var index = 0;

while (true)
{
    var current = route[index % route.Count];
    var next = route[(index + 1) % route.Count];
    var heading = CalculateHeading(current.Latitude, current.Longitude, next.Latitude, next.Longitude);
    var payload = new TelemetryPayload(
        organizationId,
        vehicleId,
        deviceId,
        DateTimeOffset.UtcNow,
        current.Latitude,
        current.Longitude,
        36 + (index % 12),
        heading);

    if (dryRun)
    {
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(payload));
    }
    else
    {
        try
        {
            using var response = await http.PostAsJsonAsync("/api/simulation/telemetry", payload);
            Console.WriteLine($"{payload.RecordedAtUtc:O} {payload.Latitude:F6},{payload.Longitude:F6} => {(int)response.StatusCode}");
        }
        catch (HttpRequestException exception)
        {
            Console.Error.WriteLine($"API unavailable: {exception.Message}");
        }
    }

    index++;
    await Task.Delay(TimeSpan.FromSeconds(1));
}

static Guid ParseGuid(string variable, string fallback) =>
    Guid.Parse(Environment.GetEnvironmentVariable(variable) ?? fallback);

static List<(double Latitude, double Longitude)> BuildRoute(double centerLat, double centerLon, double radius, int count) =>
    Enumerable.Range(0, count)
        .Select(i => i * 2 * Math.PI / count)
        .Select(angle => (
            centerLat + radius * Math.Sin(angle),
            centerLon + radius * Math.Cos(angle)))
        .ToList();

static double CalculateHeading(double lat1, double lon1, double lat2, double lon2)
{
    var phi1 = lat1 * Math.PI / 180;
    var phi2 = lat2 * Math.PI / 180;
    var deltaLambda = (lon2 - lon1) * Math.PI / 180;
    var y = Math.Sin(deltaLambda) * Math.Cos(phi2);
    var x = Math.Cos(phi1) * Math.Sin(phi2) - Math.Sin(phi1) * Math.Cos(phi2) * Math.Cos(deltaLambda);
    return (Math.Atan2(y, x) * 180 / Math.PI + 360) % 360;
}

internal sealed record TelemetryPayload(
    Guid OrganizationId,
    Guid VehicleId,
    string DeviceId,
    DateTimeOffset RecordedAtUtc,
    double Latitude,
    double Longitude,
    double SpeedKph,
    double HeadingDegrees);
