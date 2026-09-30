using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using FleetOps.Api.Tracking;
using FleetOps.UnitTests.Infrastructure;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Xunit;

namespace FleetOps.UnitTests;

[Trait("Category", "Reliability")]
public sealed class SignalRLifecycleReliabilityTests
{
    [Fact]
    public async Task BrowserDisconnectReconnectRestoresCatchUpAndLiveUpdates()
    {
        await using var factory = new FleetOpsApiFactory();
        await factory.InitializeAsync();
        using var client = factory.CreateClient();
        var scenario = await LoadScenarioAsync(client, "northwind", 3);
        var vehicle = scenario.Vehicles[0];
        var login = await client.LoginAsync("operator@northwind.local", "Operator123!");
        var baseUtc = DateTimeOffset.UtcNow.AddMinutes(-5);

        Assert.Equal(HttpStatusCode.Accepted, (await PostTelemetryAsync(client, scenario, vehicle, "hub-1", baseUtc.AddSeconds(5), 1)).StatusCode);

        var pending = new ConcurrentDictionary<long, TaskCompletionSource<TrackingPositionResponse>>();
        await using var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(client.BaseAddress!, "/hubs/tracking"), options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.AccessTokenProvider = () => Task.FromResult<string?>(login.AccessToken);
            })
            .Build();
        connection.On<TrackingPositionResponse>("trackingPositionChanged", position =>
        {
            if (position.VehicleId == vehicle.VehicleId
                && position.SequenceNumber.HasValue
                && pending.TryRemove(position.SequenceNumber.Value, out var completion))
            {
                completion.TrySetResult(position);
            }
        });

        await connection.StartAsync();
        var live = Expect(pending, 2);
        Assert.Equal(HttpStatusCode.Accepted, (await PostTelemetryAsync(client, scenario, vehicle, "hub-2", baseUtc.AddSeconds(10), 2)).StatusCode);
        Assert.Equal(2, (await live.WaitAsync(TimeSpan.FromSeconds(20))).SequenceNumber);

        await connection.StopAsync();
        Assert.Equal(HttpStatusCode.Accepted, (await PostTelemetryAsync(client, scenario, vehicle, "hub-3", baseUtc.AddSeconds(15), 3)).StatusCode);

        await connection.StartAsync();
        client.SetBearer(login.AccessToken);
        var positions = await client.GetFromJsonAsync<List<TrackingPositionResponse>>("/api/v1/tracking/positions");
        var catchUp = Assert.Single(positions!, x => x.VehicleId == vehicle.VehicleId);
        Assert.Equal(3, catchUp.SequenceNumber);

        var liveAfterReconnect = Expect(pending, 4);
        Assert.Equal(HttpStatusCode.Accepted, (await PostTelemetryAsync(client, scenario, vehicle, "hub-4", baseUtc.AddSeconds(20), 4)).StatusCode);
        Assert.Equal(4, (await liveAfterReconnect.WaitAsync(TimeSpan.FromSeconds(20))).SequenceNumber);
    }

    private static Task<TrackingPositionResponse> Expect(ConcurrentDictionary<long, TaskCompletionSource<TrackingPositionResponse>> pending, long sequence)
    {
        var completion = new TaskCompletionSource<TrackingPositionResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        pending[sequence] = completion;
        return completion.Task;
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
