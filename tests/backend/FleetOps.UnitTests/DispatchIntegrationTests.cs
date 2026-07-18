using System.Net;
using System.Net.Http.Json;
using FleetOps.Api.Dispatch;
using FleetOps.Api.Tracking;
using FleetOps.Core.Modules.Dispatch;
using FleetOps.Infrastructure.Identity;
using FleetOps.Infrastructure.Persistence;
using FleetOps.UnitTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FleetOps.UnitTests;

public sealed class DispatchIntegrationTests(FleetOpsApiFactory factory) : IClassFixture<FleetOpsApiFactory>
{
    [Fact]
    public async Task OperatorCanCreateAssignTrackAndCompleteMission()
    {
        await ResetDatabaseAsync();

        using var client = factory.CreateClient();
        var login = await client.LoginAsync("operator@northwind.local", "Operator123!");
        client.SetBearer(login.AccessToken);

        var mission = await CreateMissionAsync(client, "NW-M-100", "Morning downtown loop");
        Assert.Equal(MissionStatus.Draft, mission.Status);

        var assigned = await ProgressMissionToAssignedAsync(client, mission);
        Assert.Equal(MissionStatus.Assigned, assigned.Status);

        var telemetryClient = factory.CreateClient();
        var scenario = await ResetAndLoadScenarioAsync(telemetryClient, "northwind");
        var telemetryVehicle = Assert.Single(scenario.Vehicles, x => x.VehicleId == assigned.VehicleId);
        var telemetryResponse = await telemetryClient.PostAsJsonAsync(
            "/api/internal/v1/tracking/events",
            new IngestTelemetryRequest(
                scenario.OrganizationId,
                telemetryVehicle.VehicleId,
                telemetryVehicle.DeviceId,
                "dispatch-map-link",
                DateTimeOffset.UtcNow,
                48.401,
                9.204,
                37,
                95));
        Assert.Equal(HttpStatusCode.Accepted, telemetryResponse.StatusCode);

        var enRouteResponse = await client.PostAsJsonAsync(
            $"/api/v1/dispatch/missions/{assigned.Id}/status",
            new TransitionMissionStatusRequest(MissionStatus.EnRoute, assigned.RowVersion));
        var enRoute = await enRouteResponse.Content.ReadFromJsonAsync<MissionDetailResponse>();
        Assert.Equal(HttpStatusCode.OK, enRouteResponse.StatusCode);

        var arrivedResponse = await client.PostAsJsonAsync(
            $"/api/v1/dispatch/missions/{enRoute!.Id}/status",
            new TransitionMissionStatusRequest(MissionStatus.Arrived, enRoute.RowVersion));
        var arrived = await arrivedResponse.Content.ReadFromJsonAsync<MissionDetailResponse>();
        Assert.Equal(HttpStatusCode.OK, arrivedResponse.StatusCode);

        var completedResponse = await client.PostAsJsonAsync(
            $"/api/v1/dispatch/missions/{arrived!.Id}/status",
            new TransitionMissionStatusRequest(MissionStatus.Completed, arrived.RowVersion));
        var completed = await completedResponse.Content.ReadFromJsonAsync<MissionDetailResponse>();
        Assert.Equal(HttpStatusCode.OK, completedResponse.StatusCode);
        Assert.NotNull(completed);
        Assert.Equal(MissionStatus.Completed, completed!.Status);
        Assert.Contains(completed.Timeline, x => x.EventType == MissionTimelineEventType.Created);
        Assert.Contains(completed.Timeline, x => x.Description.Contains("Assigned", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(completed.Timeline, x => x.Description.Contains("Completed", StringComparison.OrdinalIgnoreCase));

        var list = await client.GetFromJsonAsync<List<MissionSummaryResponse>>("/api/v1/dispatch/missions");
        var listedMission = Assert.Single(list!, x => x.Id == completed.Id);
        Assert.Equal("NW-100", listedMission.VehicleRegistrationNumber);
        Assert.Equal(48.401, listedMission.CurrentLatitude);
        Assert.Equal(9.204, listedMission.CurrentLongitude);
    }

    [Fact]
    public async Task IllegalTransitionReturnsBadRequest()
    {
        await ResetDatabaseAsync();

        using var client = factory.CreateClient();
        var login = await client.LoginAsync("operator@northwind.local", "Operator123!");
        client.SetBearer(login.AccessToken);

        var mission = await CreateMissionAsync(client, "NW-M-ILLEGAL", "Illegal transition demo");

        var response = await client.PostAsJsonAsync(
            $"/api/v1/dispatch/missions/{mission.Id}/status",
            new TransitionMissionStatusRequest(MissionStatus.Completed, mission.RowVersion));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AssignmentRejectsCrossTenantDriver()
    {
        await ResetDatabaseAsync();

        using var northClient = factory.CreateClient();
        var northLogin = await northClient.LoginAsync("operator@northwind.local", "Operator123!");
        northClient.SetBearer(northLogin.AccessToken);
        var mission = await CreateMissionAsync(northClient, "NW-M-TENANT", "Tenant-safe mission");
        var plannedResponse = await northClient.PostAsJsonAsync(
            $"/api/v1/dispatch/missions/{mission.Id}/status",
            new TransitionMissionStatusRequest(MissionStatus.Planned, mission.RowVersion));
        var planned = await plannedResponse.Content.ReadFromJsonAsync<MissionDetailResponse>();

        using var southClient = factory.CreateClient();
        var southLogin = await southClient.LoginAsync("admin@southridge.local", "Admin123!");
        southClient.SetBearer(southLogin.AccessToken);
        var southDrivers = await southClient.GetFromJsonAsync<List<DriverAssignmentCandidate>>("/api/v1/fleet/drivers");
        var southVehicles = await southClient.GetFromJsonAsync<List<VehicleAssignmentCandidate>>("/api/v1/fleet/vehicles");
        var southDriver = Assert.Single(southDrivers!);
        var southVehicle = Assert.Single(southVehicles!, vehicle => vehicle.RegistrationNumber == "SR-200");

        var assignmentResponse = await northClient.PutAsJsonAsync(
            $"/api/v1/dispatch/missions/{planned!.Id}/assignment",
            new SetMissionAssignmentRequest(southDriver.Id, southVehicle.Id, planned.RowVersion));

        Assert.Equal(HttpStatusCode.BadRequest, assignmentResponse.StatusCode);
    }

    [Fact]
    public async Task OverlappingMissionAssignmentReturnsConflict()
    {
        await ResetDatabaseAsync();

        using var client = factory.CreateClient();
        var login = await client.LoginAsync("operator@northwind.local", "Operator123!");
        client.SetBearer(login.AccessToken);

        var firstMission = await ProgressMissionToAssignedAsync(
            client,
            await CreateMissionAsync(client, "NW-M-C1", "Conflict one"));

        var secondMission = await CreateMissionAsync(client, "NW-M-C2", "Conflict two");
        var secondPlannedResponse = await client.PostAsJsonAsync(
            $"/api/v1/dispatch/missions/{secondMission.Id}/status",
            new TransitionMissionStatusRequest(MissionStatus.Planned, secondMission.RowVersion));
        var secondPlanned = await secondPlannedResponse.Content.ReadFromJsonAsync<MissionDetailResponse>();

        var conflictResponse = await client.PutAsJsonAsync(
            $"/api/v1/dispatch/missions/{secondPlanned!.Id}/assignment",
            new SetMissionAssignmentRequest(firstMission.DriverId!.Value, firstMission.VehicleId!.Value, secondPlanned.RowVersion));

        Assert.Equal(HttpStatusCode.Conflict, conflictResponse.StatusCode);
    }

    private static async Task<MissionDetailResponse> ProgressMissionToAssignedAsync(HttpClient client, MissionDetailResponse mission)
    {
        var plannedResponse = await client.PostAsJsonAsync(
            $"/api/v1/dispatch/missions/{mission.Id}/status",
            new TransitionMissionStatusRequest(MissionStatus.Planned, mission.RowVersion));
        if (plannedResponse.StatusCode != HttpStatusCode.OK)
        {
            Assert.Fail(await plannedResponse.Content.ReadAsStringAsync());
        }

        var planned = await plannedResponse.Content.ReadFromJsonAsync<MissionDetailResponse>();

        var drivers = await client.GetFromJsonAsync<List<DriverAssignmentCandidate>>("/api/v1/fleet/drivers");
        var vehicles = await client.GetFromJsonAsync<List<VehicleAssignmentCandidate>>("/api/v1/fleet/vehicles");
        var driver = Assert.Single(drivers!, x => x.LicenseNumber == "NW-DL-001");
        var vehicle = Assert.Single(vehicles!, x => x.RegistrationNumber == "NW-100");

        var assignmentResponse = await client.PutAsJsonAsync(
            $"/api/v1/dispatch/missions/{planned!.Id}/assignment",
            new SetMissionAssignmentRequest(driver.Id, vehicle.Id, planned.RowVersion));
        if (assignmentResponse.StatusCode != HttpStatusCode.OK)
        {
            Assert.Fail(await assignmentResponse.Content.ReadAsStringAsync());
        }

        var assigned = await assignmentResponse.Content.ReadFromJsonAsync<MissionDetailResponse>();

        var assignedStatusResponse = await client.PostAsJsonAsync(
            $"/api/v1/dispatch/missions/{assigned!.Id}/status",
            new TransitionMissionStatusRequest(MissionStatus.Assigned, assigned.RowVersion));
        var progressed = await assignedStatusResponse.Content.ReadFromJsonAsync<MissionDetailResponse>();
        Assert.Equal(HttpStatusCode.OK, assignedStatusResponse.StatusCode);
        return progressed!;
    }

    private static async Task<MissionDetailResponse> CreateMissionAsync(HttpClient client, string reference, string title)
    {
        var start = DateTimeOffset.UtcNow.AddHours(2);
        var response = await client.PostAsJsonAsync(
            "/api/v1/dispatch/missions",
            new CreateMissionRequest(
                reference,
                title,
                start,
                start.AddHours(2),
                [
                    new MissionStopRequest(1, "Depot", "1 Dispatch Way", start.AddMinutes(30)),
                    new MissionStopRequest(2, "Customer", "22 Fleet Street", start.AddMinutes(90))
                ]));
        var mission = await response.Content.ReadFromJsonAsync<MissionDetailResponse>();
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(mission);
        Assert.Equal(2, mission!.Stops.Count);
        return mission!;
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
        await FleetOpsSeedData.EnsureSeededAsync(dbContext, roleManager, userManager, new BootstrapOptions { SeedDemoData = true }, CancellationToken.None);
    }

    private sealed record DriverAssignmentCandidate(Guid Id, string FullName, string LicenseNumber, string? PhoneNumber, bool IsActive, long RowVersion);
    private sealed record VehicleAssignmentCandidate(Guid Id, string RegistrationNumber, string DisplayName, bool IsActive, long RowVersion);
}
