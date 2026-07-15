using System.Net;
using System.Net.Http.Json;
using FleetOps.Api.Tracking;
using FleetOps.UnitTests.Infrastructure;
using Xunit;

namespace FleetOps.UnitTests;

public sealed class TrackingIntegrationTests(FleetOpsApiFactory factory) : IClassFixture<FleetOpsApiFactory>
{
    [Fact]
    public async Task ScenarioCanDisplayThreeVehiclesSimultaneously()
    {
        using var client = factory.CreateClient();
        var scenario = await ResetAndLoadScenarioAsync(client, "northwind");

        for (var i = 0; i < 3; i++)
        {
            var vehicle = scenario.Vehicles[i];
            var response = await client.PostAsJsonAsync("/api/internal/v1/tracking/events", new IngestTelemetryRequest(
                scenario.OrganizationId,
                vehicle.VehicleId,
                vehicle.DeviceId,
                $"three-vehicles-{i}",
                DateTimeOffset.UtcNow.AddSeconds(i),
                48.40 + i * 0.01,
                9.20 + i * 0.01,
                40 + i,
                90 + i * 5));
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        }

        using var webClient = factory.CreateClient();
        var login = await webClient.LoginAsync("admin@northwind.local", "Admin123!");
        webClient.SetBearer(login.AccessToken);

        var positions = await webClient.GetFromJsonAsync<List<TrackingPositionResponse>>("/api/v1/tracking/positions");

        Assert.NotNull(positions);
        Assert.Equal(3, positions!.Count);
    }

    [Fact]
    public async Task DuplicateEventDoesNotCreateSecondHistoryPoint()
    {
        using var client = factory.CreateClient();
        var scenario = await ResetAndLoadScenarioAsync(client, "northwind");
        var vehicle = scenario.Vehicles[0];
        var recordedAt = DateTimeOffset.UtcNow;
        var request = new IngestTelemetryRequest(
            scenario.OrganizationId,
            vehicle.VehicleId,
            vehicle.DeviceId,
            "duplicate-1",
            recordedAt,
            48.4,
            9.2,
            30,
            180);

        var first = await client.PostAsJsonAsync("/api/internal/v1/tracking/events", request);
        var duplicate = await client.PostAsJsonAsync("/api/internal/v1/tracking/events", request);

        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, duplicate.StatusCode);

        using var webClient = factory.CreateClient();
        var login = await webClient.LoginAsync("admin@northwind.local", "Admin123!");
        webClient.SetBearer(login.AccessToken);

        var history = await webClient.GetFromJsonAsync<TrackingHistoryPageResponse>(
            $"/api/v1/tracking/history?vehicleId={vehicle.VehicleId}&page=1&pageSize=20");
        var metrics = await webClient.GetFromJsonAsync<TrackingMetricsResponse>("/api/v1/tracking/metrics");

        Assert.NotNull(history);
        Assert.Single(history!.Items);
        Assert.NotNull(metrics);
        Assert.Equal(1, metrics!.DuplicateCount);
    }

    [Fact]
    public async Task OutOfOrderPointDoesNotReplaceCurrentPosition()
    {
        using var client = factory.CreateClient();
        var scenario = await ResetAndLoadScenarioAsync(client, "northwind");
        var vehicle = scenario.Vehicles[0];
        var now = DateTimeOffset.UtcNow;

        await client.PostAsJsonAsync("/api/internal/v1/tracking/events", new IngestTelemetryRequest(
            scenario.OrganizationId,
            vehicle.VehicleId,
            vehicle.DeviceId,
            "latest-point",
            now,
            48.41,
            9.21,
            35,
            110));

        await client.PostAsJsonAsync("/api/internal/v1/tracking/events", new IngestTelemetryRequest(
            scenario.OrganizationId,
            vehicle.VehicleId,
            vehicle.DeviceId,
            "older-point",
            now.AddMinutes(-5),
            48.31,
            9.11,
            20,
            75));

        using var webClient = factory.CreateClient();
        var login = await webClient.LoginAsync("operator@northwind.local", "Operator123!");
        webClient.SetBearer(login.AccessToken);

        var positions = await webClient.GetFromJsonAsync<List<TrackingPositionResponse>>("/api/v1/tracking/positions");
        var metrics = await webClient.GetFromJsonAsync<TrackingMetricsResponse>("/api/v1/tracking/metrics");
        var current = Assert.Single(positions!, x => x.VehicleId == vehicle.VehicleId);

        Assert.Equal(48.41, current.Latitude);
        Assert.Equal(9.21, current.Longitude);
        Assert.NotNull(metrics);
        Assert.Equal(1, metrics!.OutOfOrderCount);
    }

    [Fact]
    public async Task HistoryIsPaginated()
    {
        using var client = factory.CreateClient();
        var scenario = await ResetAndLoadScenarioAsync(client, "northwind");
        var vehicle = scenario.Vehicles[0];
        var start = DateTimeOffset.UtcNow;

        for (var i = 0; i < 5; i++)
        {
            await client.PostAsJsonAsync("/api/internal/v1/tracking/events", new IngestTelemetryRequest(
                scenario.OrganizationId,
                vehicle.VehicleId,
                vehicle.DeviceId,
                $"history-{i}",
                start.AddSeconds(i),
                48.4 + i * 0.001,
                9.2 + i * 0.001,
                25 + i,
                45 + i));
        }

        using var webClient = factory.CreateClient();
        var login = await webClient.LoginAsync("admin@northwind.local", "Admin123!");
        webClient.SetBearer(login.AccessToken);

        var page1 = await webClient.GetFromJsonAsync<TrackingHistoryPageResponse>(
            $"/api/v1/tracking/history?vehicleId={vehicle.VehicleId}&page=1&pageSize=2");
        var page2 = await webClient.GetFromJsonAsync<TrackingHistoryPageResponse>(
            $"/api/v1/tracking/history?vehicleId={vehicle.VehicleId}&page=2&pageSize=2");

        Assert.NotNull(page1);
        Assert.NotNull(page2);
        Assert.Equal(5, page1!.TotalCount);
        Assert.Equal(2, page1.Items.Count);
        Assert.Equal(2, page2!.Items.Count);
        Assert.NotEqual(page1.Items[0].EventId, page2.Items[0].EventId);
    }

    private static async Task<TrackingScenarioResponse> ResetAndLoadScenarioAsync(HttpClient client, string organizationSlug)
    {
        var reset = await client.PostAsync($"/api/internal/v1/tracking/scenarios/{organizationSlug}/reset", null);
        reset.EnsureSuccessStatusCode();

        var scenario = await client.GetFromJsonAsync<TrackingScenarioResponse>(
            $"/api/internal/v1/tracking/scenarios/{organizationSlug}");
        Assert.NotNull(scenario);
        return scenario!;
    }
}
