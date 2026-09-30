using System.Net;
using System.Net.Http.Json;
using FleetOps.Api.Dispatch;
using FleetOps.Api.Tracking;
using FleetOps.Core.Modules.Demo;
using FleetOps.Core.Modules.Dispatch;
using FleetOps.Infrastructure.Identity;
using FleetOps.Infrastructure.Persistence;
using FleetOps.UnitTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FleetOps.UnitTests;

public sealed class DemoScenarioIntegrationTests(FleetOpsApiFactory factory) : IClassFixture<FleetOpsApiFactory>
{
    [Fact]
    public async Task DemoTelemetryTraversesCanonicalIngestionAndRemainsTenantScoped()
    {
        await ResetDatabaseAsync();
        using var client = factory.CreateClient();
        var northwind = await LoadScenarioAsync(client, "northwind", 20);
        Assert.Equal(20, northwind.Vehicles.Count);
        var northVehicle = northwind.Vehicles[0];
        var engine = new DeterministicDemoScenarioEngine(new DeterministicDemoClock(DateTimeOffset.UtcNow));
        engine.Start(DemoScenarioKind.NormalShift, 2701);
        var emitter = new ApiTelemetryEmitter(client);
        var simulator = new DemoFleetSimulator(engine, new DemoScenarioCatalog(), emitter);

        var count = await simulator.TickAsync(
            northwind.OrganizationId,
            [new DemoVehicleBinding(northVehicle.VehicleId, northVehicle.DeviceId)],
            TimeSpan.FromSeconds(5),
            default);

        Assert.Equal(1, count);
        var login = await client.LoginAsync("operator@northwind.local", "Operator123!");
        client.SetBearer(login.AccessToken);
        var positions = await client.GetFromJsonAsync<List<TrackingPositionResponse>>("/api/v1/tracking/positions");
        Assert.Contains(positions!, position => position.VehicleId == northVehicle.VehicleId && position.Source == "demo-engine");

        var start = DateTimeOffset.UtcNow.AddHours(1);
        var createResponse = await client.PostAsJsonAsync("/api/v1/dispatch/missions", new CreateMissionRequest(
            "DEMO-ENGINE-27",
            "Demo engine workflow proof",
            start,
            start.AddHours(2),
            [new MissionStopRequest(1, "Depot", "1 Synthetic Way", start.AddMinutes(30))]));
        createResponse.EnsureSuccessStatusCode();
        var mission = (await createResponse.Content.ReadFromJsonAsync<MissionDetailResponse>())!;
        IDemoMissionEmitter missionEmitter = new ApiMissionEmitter(client);
        var missionResult = await missionEmitter.TransitionAsync(new(mission.Id, MissionStatus.Planned, mission.RowVersion), default);
        Assert.Equal(MissionStatus.Planned, missionResult.Status);

        var southridge = await LoadScenarioAsync(client, "southridge");
        var crossTenant = new DemoTelemetryEvent(
            southridge.OrganizationId,
            northVehicle.VehicleId,
            northVehicle.DeviceId,
            "demo-cross-tenant",
            DateTimeOffset.UtcNow,
            48.77,
            9.18,
            30,
            90,
            1);
        var response = await emitter.SendAsync(crossTenant, default);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<TrackingScenarioResponse> LoadScenarioAsync(HttpClient client, string slug, int maxVehicles = 3)
    {
        var reset = await client.PostAsync($"/api/internal/v1/tracking/scenarios/{slug}/reset", null);
        reset.EnsureSuccessStatusCode();
        return (await client.GetFromJsonAsync<TrackingScenarioResponse>($"/api/internal/v1/tracking/scenarios/{slug}?maxVehicles={maxVehicles}"))!;
    }

    private async Task ResetDatabaseAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FleetOpsDbContext>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var metricsStore = scope.ServiceProvider.GetRequiredService<TrackingMetricsStore>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();
        metricsStore.ResetAll();
        await FleetOpsSeedData.EnsureSeededAsync(dbContext, roleManager, userManager, new BootstrapOptions { SeedDemoData = true }, default);
    }

    private sealed class ApiTelemetryEmitter(HttpClient client) : IDemoTelemetryEmitter
    {
        public async Task EmitAsync(DemoTelemetryEvent telemetry, CancellationToken cancellationToken)
        {
            using var response = await SendAsync(telemetry, cancellationToken);
            response.EnsureSuccessStatusCode();
        }

        public Task<HttpResponseMessage> SendAsync(DemoTelemetryEvent telemetry, CancellationToken cancellationToken) =>
            client.PostAsJsonAsync("/api/internal/v1/tracking/events", new IngestTelemetryRequest(
                telemetry.OrganizationId,
                telemetry.VehicleId,
                telemetry.DeviceId,
                telemetry.EventId,
                telemetry.RecordedAtUtc,
                telemetry.Latitude,
                telemetry.Longitude,
                telemetry.SpeedKph,
                telemetry.HeadingDegrees,
                telemetry.SequenceNumber,
                telemetry.AccuracyMeters,
                telemetry.Source), cancellationToken);
    }

    private sealed class ApiMissionEmitter(HttpClient client) : IDemoMissionEmitter
    {
        public async Task<DemoMissionActionResult> TransitionAsync(DemoMissionAction action, CancellationToken cancellationToken)
        {
            using var response = await client.PostAsJsonAsync(
                $"/api/v1/dispatch/missions/{action.MissionId}/status",
                new TransitionMissionStatusRequest(action.TargetStatus, action.RowVersion),
                cancellationToken);
            response.EnsureSuccessStatusCode();
            var mission = (await response.Content.ReadFromJsonAsync<MissionDetailResponse>(cancellationToken: cancellationToken))!;
            return new(mission.Id, mission.Status, mission.RowVersion);
        }
    }
}
