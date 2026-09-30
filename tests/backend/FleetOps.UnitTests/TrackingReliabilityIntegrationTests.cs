using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using FleetOps.Api.Tracking;
using FleetOps.UnitTests.Infrastructure;
using Xunit;

namespace FleetOps.UnitTests;

[Trait("Category", "Reliability")]
public sealed class TrackingReliabilityIntegrationTests
{
    [Fact]
    public async Task TwentyVehicleSustainedIngestionKeepsEveryCurrentPositionConsistent()
    {
        await using var factory = new FleetOpsApiFactory();
        await factory.InitializeAsync();
        using var client = factory.CreateClient();
        var scenario = await LoadScenarioAsync(client, "northwind", 20);
        Assert.Equal(20, scenario.Vehicles.Count);

        const int ticks = 30;
        var startUtc = DateTimeOffset.UtcNow.AddMinutes(-5);
        var stopwatch = Stopwatch.StartNew();
        for (var tick = 1; tick <= ticks; tick++)
        {
            foreach (var (vehicle, index) in scenario.Vehicles.Select((vehicle, index) => (vehicle, index)))
            {
                var response = await PostTelemetryAsync(
                    client,
                    scenario,
                    vehicle,
                    $"reliability-{index:D2}-{tick:D4}",
                    startUtc.AddSeconds(tick * 5),
                    tick);
                Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
            }
        }

        stopwatch.Stop();
        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromMinutes(2),
            $"Sustained ingestion took an unexpected {stopwatch.Elapsed}.");

        var login = await client.LoginAsync("operator@northwind.local", "Operator123!");
        client.SetBearer(login.AccessToken);

        var positions = await client.GetFromJsonAsync<List<TrackingPositionResponse>>("/api/v1/tracking/positions");
        Assert.NotNull(positions);
        Assert.Equal(20, positions!.Count);
        foreach (var position in positions)
        {
            Assert.Equal(ticks, position.SequenceNumber);
            Assert.Equal(startUtc.AddSeconds(ticks * 5), position.RecordedAtUtc);
        }

        var metrics = await client.GetFromJsonAsync<TrackingMetricsResponse>("/api/v1/tracking/metrics");
        Assert.NotNull(metrics);
        Assert.Equal(20, metrics!.CurrentVehicleCount);
        Assert.Equal(20 * ticks, metrics.HistoryPointCount);
        Assert.Equal(20 * ticks, metrics.AcceptedCount);
        Assert.Equal(0, metrics.DuplicateCount);
        Assert.Equal(0, metrics.OutOfOrderCount);
    }

    [Fact]
    public async Task DuplicateAndOutOfOrderInputsRemainIdempotentAndNeverMoveCurrentPosition()
    {
        await using var factory = new FleetOpsApiFactory();
        await factory.InitializeAsync();
        using var client = factory.CreateClient();
        var scenario = await LoadScenarioAsync(client, "northwind", 3);
        var vehicle = scenario.Vehicles[0];
        var baseUtc = DateTimeOffset.UtcNow.AddMinutes(-5);

        Assert.Equal(HttpStatusCode.Accepted, (await PostTelemetryAsync(client, scenario, vehicle, "idem-1", baseUtc.AddSeconds(5), 1)).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await PostTelemetryAsync(client, scenario, vehicle, "idem-2", baseUtc.AddSeconds(10), 2)).StatusCode);

        var duplicate = await PostTelemetryAsync(client, scenario, vehicle, "idem-1", baseUtc.AddSeconds(5), 1);
        Assert.Equal(HttpStatusCode.Accepted, duplicate.StatusCode);
        var duplicateBody = await duplicate.Content.ReadFromJsonAsync<TelemetryIngestionResponse>();
        Assert.True(duplicateBody!.Duplicate);

        var outOfOrder = await PostTelemetryAsync(client, scenario, vehicle, "idem-older", baseUtc, 0);
        Assert.Equal(HttpStatusCode.Accepted, outOfOrder.StatusCode);
        var outOfOrderBody = await outOfOrder.Content.ReadFromJsonAsync<TelemetryIngestionResponse>();
        Assert.True(outOfOrderBody!.OutOfOrder);
        Assert.False(outOfOrderBody.CurrentPositionUpdated);

        var rejected = await client.PostAsJsonAsync("/api/internal/v1/tracking/events", new IngestTelemetryRequest(
            scenario.OrganizationId,
            vehicle.VehicleId,
            vehicle.DeviceId,
            "idem-implausible",
            baseUtc.AddSeconds(12),
            10.0,
            9.1829,
            42,
            90,
            2,
            5,
            "reliability-test"));
        Assert.Equal(HttpStatusCode.Accepted, rejected.StatusCode);
        var rejectedBody = await rejected.Content.ReadFromJsonAsync<TelemetryIngestionResponse>();
        Assert.False(rejectedBody!.OutOfOrder);
        Assert.False(rejectedBody.CurrentPositionUpdated);

        var login = await client.LoginAsync("operator@northwind.local", "Operator123!");
        client.SetBearer(login.AccessToken);
        var positions = await client.GetFromJsonAsync<List<TrackingPositionResponse>>("/api/v1/tracking/positions");
        var current = Assert.Single(positions!, x => x.VehicleId == vehicle.VehicleId);
        Assert.Equal(2, current.SequenceNumber);
        Assert.Equal(baseUtc.AddSeconds(10), current.RecordedAtUtc);

        var metrics = await client.GetFromJsonAsync<TrackingMetricsResponse>("/api/v1/tracking/metrics");
        Assert.Equal(4, metrics!.AcceptedCount);
        Assert.Equal(1, metrics.DuplicateCount);
        Assert.Equal(1, metrics.OutOfOrderCount);
        Assert.Equal(2, metrics.RejectedCount);

        Assert.Equal(HttpStatusCode.Accepted, (await PostTelemetryAsync(client, scenario, vehicle, "idem-3", baseUtc.AddSeconds(15), 3)).StatusCode);
        var refreshed = await client.GetFromJsonAsync<List<TrackingPositionResponse>>("/api/v1/tracking/positions");
        Assert.Equal(3, Assert.Single(refreshed!, x => x.VehicleId == vehicle.VehicleId).SequenceNumber);
    }

    [Fact]
    public async Task TenantIsolationHoldsWhileConcurrentLoadIsApplied()
    {
        await using var factory = new FleetOpsApiFactory();
        await factory.InitializeAsync();
        using var client = factory.CreateClient();
        var northwind = await LoadScenarioAsync(client, "northwind", 20);
        var southridge = await LoadScenarioAsync(client, "southridge", 3);
        var northVehicle = northwind.Vehicles[0];
        var startUtc = DateTimeOffset.UtcNow.AddMinutes(-5);

        for (var tick = 1; tick <= 5; tick++)
        {
            foreach (var vehicle in northwind.Vehicles)
            {
                var response = await PostTelemetryAsync(client, northwind, vehicle, $"isolation-{vehicle.RegistrationNumber}-{tick}", startUtc.AddSeconds(tick * 5), tick);
                Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
            }
        }

        var northLogin = await client.LoginAsync("operator@northwind.local", "Operator123!");
        using var northClient = factory.CreateClient();
        northClient.SetBearer(northLogin.AccessToken);

        var crossTenantHistory = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ =>
            northClient.GetAsync($"/api/v1/tracking/history?vehicleId={southridge.Vehicles[0].VehicleId}&page=1&pageSize=20")));
        Assert.All(crossTenantHistory, response => Assert.Equal(HttpStatusCode.NotFound, response.StatusCode));

        var crossTenantWrites = await Task.WhenAll(Enumerable.Range(0, 20).Select(index =>
            client.PostAsJsonAsync("/api/internal/v1/tracking/events", new IngestTelemetryRequest(
                southridge.OrganizationId,
                northVehicle.VehicleId,
                northVehicle.DeviceId,
                $"isolation-cross-{index}",
                startUtc.AddMinutes(1),
                48.77,
                9.18,
                30,
                90,
                10, 5, "reliability-test"))));
        Assert.All(crossTenantWrites, response => Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode));

        var positions = await northClient.GetFromJsonAsync<List<TrackingPositionResponse>>("/api/v1/tracking/positions");
        Assert.Equal(20, positions!.Count);
        Assert.DoesNotContain(positions, position => position.VehicleId == southridge.Vehicles[0].VehicleId);
    }

    private static async Task<TrackingScenarioResponse> LoadScenarioAsync(HttpClient client, string slug, int maxVehicles)
    {
        var reset = await client.PostAsync($"/api/internal/v1/tracking/scenarios/{slug}/reset", null);
        reset.EnsureSuccessStatusCode();
        return (await client.GetFromJsonAsync<TrackingScenarioResponse>($"/api/internal/v1/tracking/scenarios/{slug}?maxVehicles={maxVehicles}"))!;
    }

    private static Task<HttpResponseMessage> PostTelemetryAsync(
        HttpClient client,
        TrackingScenarioResponse scenario,
        TrackingScenarioVehicleResponse vehicle,
        string eventId,
        DateTimeOffset recordedAtUtc,
        long sequence) =>
        client.PostAsJsonAsync("/api/internal/v1/tracking/events", new IngestTelemetryRequest(
            scenario.OrganizationId,
            vehicle.VehicleId,
            vehicle.DeviceId,
            eventId,
            recordedAtUtc,
            48.7758,
            9.1829,
            42,
            90,
            sequence,
            5,
            "reliability-test"));
}
