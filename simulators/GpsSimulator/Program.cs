using System.Net.Http.Json;
using System.Text.Json;

var arguments = args.ToHashSet(StringComparer.OrdinalIgnoreCase);
var dryRun = arguments.Contains("--dry-run");
var runOnce = arguments.Contains("--once");
var replayDuplicate = arguments.Contains("--replay-duplicate");
var sendOutOfOrder = arguments.Contains("--send-out-of-order");
var keepHistory = arguments.Contains("--keep-history");
var apiBaseUrl = Environment.GetEnvironmentVariable("FLEETOPS_API_URL") ?? "http://localhost:5080";
var organizationSlug = Environment.GetEnvironmentVariable("SIM_ORGANIZATION_SLUG") ?? "northwind";
var iterationCount = ParseInt("SIM_ITERATIONS", fallback: 20);
var intervalMs = ParseInt("SIM_INTERVAL_MS", fallback: 1000);

using var http = new HttpClient { BaseAddress = new Uri(apiBaseUrl) };
Console.WriteLine(
    $"FleetOps GPS simulator. API={apiBaseUrl}, org={organizationSlug}, dryRun={dryRun}, once={runOnce}");

var scenario = await LoadScenarioAsync(http, organizationSlug, dryRun);

if (!dryRun && !keepHistory)
{
    using var resetResponse = await http.PostAsync(
        $"/api/internal/v1/tracking/scenarios/{organizationSlug}/reset",
        content: null);
    resetResponse.EnsureSuccessStatusCode();
}

var routes = scenario.Vehicles
    .Select((vehicle, index) => new ScenarioRoute(
        vehicle,
        BuildRoute(48.4914 + index * 0.01, 9.2043 + index * 0.01, 0.01 + index * 0.001, 60)))
    .ToList();

var sentPayloads = new List<IngestTelemetryRequest>();
var loopLimit = runOnce ? 1 : iterationCount;

for (var iteration = 0; iteration < loopLimit; iteration++)
{
    foreach (var route in routes)
    {
        var current = route.Points[iteration % route.Points.Count];
        var next = route.Points[(iteration + 1) % route.Points.Count];
        var heading = CalculateHeading(current.Latitude, current.Longitude, next.Latitude, next.Longitude);
        var payload = new IngestTelemetryRequest(
            scenario.OrganizationId,
            route.Vehicle.VehicleId,
            route.Vehicle.DeviceId,
            $"{route.Vehicle.VehicleId:N}:{iteration}",
            DateTimeOffset.UtcNow.AddSeconds(iteration),
            current.Latitude,
            current.Longitude,
            34 + (iteration % 8) + routes.IndexOf(route),
            heading);

        sentPayloads.Add(payload);
        await EmitAsync(http, payload, dryRun);
    }

    if (!runOnce)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(intervalMs));
    }
}

if (sentPayloads.Count > 0 && replayDuplicate)
{
    await EmitAsync(http, sentPayloads[0], dryRun);
}

if (sentPayloads.Count > 0 && sendOutOfOrder)
{
    var seed = sentPayloads[0];
    var outOfOrder = seed with
    {
        EventId = $"{seed.EventId}:older",
        RecordedAtUtc = seed.RecordedAtUtc.AddMinutes(-5),
    };
    await EmitAsync(http, outOfOrder, dryRun);
}

static async Task EmitAsync(HttpClient http, IngestTelemetryRequest payload, bool dryRun)
{
    if (dryRun)
    {
        Console.WriteLine(JsonSerializer.Serialize(payload));
        return;
    }

    try
    {
        using var response = await http.PostAsJsonAsync("/api/internal/v1/tracking/events", payload);
        Console.WriteLine(
            $"{payload.RecordedAtUtc:O} {payload.DeviceId} {payload.Latitude:F6},{payload.Longitude:F6} => {(int)response.StatusCode}");
    }
    catch (HttpRequestException exception)
    {
        Console.Error.WriteLine($"API unavailable: {exception.Message}");
    }
}

static int ParseInt(string variable, int fallback)
{
    var raw = Environment.GetEnvironmentVariable(variable);
    return int.TryParse(raw, out var value) && value > 0 ? value : fallback;
}

static List<(double Latitude, double Longitude)> BuildRoute(
    double centerLat,
    double centerLon,
    double radius,
    int count) =>
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

static async Task<TrackingScenarioResponse> LoadScenarioAsync(
    HttpClient http,
    string organizationSlug,
    bool dryRun)
{
    if (dryRun)
    {
        return new TrackingScenarioResponse(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "Dry Run Logistics",
            organizationSlug,
            [
                new TrackingScenarioVehicleResponse(
                    Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1"),
                    "SIM-100",
                    "Dry Run Vehicle 1",
                    "SIM-GPS-100"),
                new TrackingScenarioVehicleResponse(
                    Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2"),
                    "SIM-101",
                    "Dry Run Vehicle 2",
                    "SIM-GPS-101"),
                new TrackingScenarioVehicleResponse(
                    Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa3"),
                    "SIM-102",
                    "Dry Run Vehicle 3",
                    "SIM-GPS-102"),
            ]);
    }

    try
    {
        var remoteScenario = await http.GetFromJsonAsync<TrackingScenarioResponse>(
            $"/api/internal/v1/tracking/scenarios/{organizationSlug}");
        if (remoteScenario is not null)
        {
            return remoteScenario;
        }
    }
    catch (HttpRequestException)
    {
        throw new InvalidOperationException("Could not load the tracking scenario.");
    }

    throw new InvalidOperationException("Could not load the tracking scenario.");
}

internal sealed record TrackingScenarioResponse(
    Guid OrganizationId,
    string OrganizationName,
    string OrganizationSlug,
    IReadOnlyList<TrackingScenarioVehicleResponse> Vehicles);

internal sealed record TrackingScenarioVehicleResponse(
    Guid VehicleId,
    string RegistrationNumber,
    string DisplayName,
    string DeviceId);

internal sealed record IngestTelemetryRequest(
    Guid OrganizationId,
    Guid VehicleId,
    string DeviceId,
    string EventId,
    DateTimeOffset RecordedAtUtc,
    double Latitude,
    double Longitude,
    double SpeedKph,
    double HeadingDegrees);

internal sealed record ScenarioRoute(
    TrackingScenarioVehicleResponse Vehicle,
    IReadOnlyList<(double Latitude, double Longitude)> Points);
