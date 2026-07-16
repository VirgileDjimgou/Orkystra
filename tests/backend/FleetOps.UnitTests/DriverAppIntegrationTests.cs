using System.Net;
using System.Net.Http.Json;
using FleetOps.Api.Dispatch;
using FleetOps.Api.DriverApp;
using FleetOps.Core.Modules.Dispatch;
using FleetOps.Infrastructure.Identity;
using FleetOps.Infrastructure.Persistence;
using FleetOps.UnitTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FleetOps.UnitTests;

public sealed class DriverAppIntegrationTests(FleetOpsApiFactory factory) : IClassFixture<FleetOpsApiFactory>
{
    [Fact]
    public async Task DriverOnlySeesAssignedOwnMissions()
    {
        await ResetDatabaseAsync();

        using var operatorClient = factory.CreateClient();
        var operatorLogin = await operatorClient.LoginAsync("operator@northwind.local", "Operator123!");
        operatorClient.SetBearer(operatorLogin.AccessToken);

        var assignedMission = await ProgressMissionToAssignedAsync(
            operatorClient,
            await CreateMissionAsync(operatorClient, "NW-M-DRV-1", "Driver visible mission"));
        await CreateMissionAsync(operatorClient, "NW-M-DRV-2", "Unassigned mission");

        using var driverClient = factory.CreateClient();
        var driverLogin = await driverClient.LoginAsync("driver@northwind.local", "Driver123!");
        driverClient.SetBearer(driverLogin.AccessToken);

        var missions = await driverClient.GetFromJsonAsync<List<DriverMissionSummaryResponse>>("/api/v1/driver/missions");

        var listedMission = Assert.Single(missions!);
        Assert.Equal(assignedMission.Id, listedMission.Id);
        Assert.Equal("NW-100", listedMission.VehicleRegistrationNumber);
        Assert.True(driverLogin.User.DriverId.HasValue);
    }

    [Fact]
    public async Task DuplicateCommandIsIdempotent()
    {
        await ResetDatabaseAsync();

        using var operatorClient = factory.CreateClient();
        var operatorLogin = await operatorClient.LoginAsync("operator@northwind.local", "Operator123!");
        operatorClient.SetBearer(operatorLogin.AccessToken);

        var assignedMission = await ProgressMissionToAssignedAsync(
            operatorClient,
            await CreateMissionAsync(operatorClient, "NW-M-DRV-IDEMP", "Idempotent sync mission"));

        using var driverClient = factory.CreateClient();
        var driverLogin = await driverClient.LoginAsync("driver@northwind.local", "Driver123!");
        driverClient.SetBearer(driverLogin.AccessToken);

        var request = new SyncMissionCommandRequest(
            "sync-start-001",
            DriverMissionCommandAction.Start,
            assignedMission.RowVersion,
            DateTimeOffset.UtcNow);

        var firstResponse = await driverClient.PostAsJsonAsync(
            $"/api/v1/driver/missions/{assignedMission.Id}/commands",
            request);
        var firstPayload = await firstResponse.Content.ReadFromJsonAsync<SyncMissionCommandResponse>();

        var secondResponse = await driverClient.PostAsJsonAsync(
            $"/api/v1/driver/missions/{assignedMission.Id}/commands",
            request);
        var secondPayload = await secondResponse.Content.ReadFromJsonAsync<SyncMissionCommandResponse>();

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.NotNull(firstPayload);
        Assert.NotNull(secondPayload);
        Assert.Equal(MissionStatus.EnRoute, firstPayload!.Mission.Status);
        Assert.False(firstPayload.WasDuplicate);
        Assert.True(secondPayload!.WasDuplicate);
        Assert.Equal(MissionStatus.EnRoute, secondPayload.Mission.Status);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FleetOpsDbContext>();
        var mission = await dbContext.Missions.SingleAsync(x => x.Id == assignedMission.Id);
        var receipts = dbContext.DriverSyncCommandReceipts.Where(x => x.MissionId == assignedMission.Id).ToList();

        Assert.Equal(MissionStatus.EnRoute, mission.Status);
        Assert.Single(receipts);
    }

    [Fact]
    public async Task StaleRowVersionReturnsConflict()
    {
        await ResetDatabaseAsync();

        using var operatorClient = factory.CreateClient();
        var operatorLogin = await operatorClient.LoginAsync("operator@northwind.local", "Operator123!");
        operatorClient.SetBearer(operatorLogin.AccessToken);

        var assignedMission = await ProgressMissionToAssignedAsync(
            operatorClient,
            await CreateMissionAsync(operatorClient, "NW-M-DRV-STALE", "Stale sync mission"));

        using var driverClient = factory.CreateClient();
        var driverLogin = await driverClient.LoginAsync("driver@northwind.local", "Driver123!");
        driverClient.SetBearer(driverLogin.AccessToken);

        var response = await driverClient.PostAsJsonAsync(
            $"/api/v1/driver/missions/{assignedMission.Id}/commands",
            new SyncMissionCommandRequest(
                "sync-stale-001",
                DriverMissionCommandAction.Start,
                assignedMission.RowVersion - 1,
                DateTimeOffset.UtcNow));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    private static async Task<MissionDetailResponse> ProgressMissionToAssignedAsync(HttpClient client, MissionDetailResponse mission)
    {
        var plannedResponse = await client.PostAsJsonAsync(
            $"/api/v1/dispatch/missions/{mission.Id}/status",
            new TransitionMissionStatusRequest(MissionStatus.Planned, mission.RowVersion));
        var planned = await plannedResponse.Content.ReadFromJsonAsync<MissionDetailResponse>();
        Assert.Equal(HttpStatusCode.OK, plannedResponse.StatusCode);

        var drivers = await client.GetFromJsonAsync<List<DriverAssignmentCandidate>>("/api/v1/fleet/drivers");
        var vehicles = await client.GetFromJsonAsync<List<VehicleAssignmentCandidate>>("/api/v1/fleet/vehicles");
        var driver = Assert.Single(drivers!, x => x.LicenseNumber == "NW-DL-001");
        var vehicle = Assert.Single(vehicles!, x => x.RegistrationNumber == "NW-100");

        var assignmentResponse = await client.PutAsJsonAsync(
            $"/api/v1/dispatch/missions/{planned!.Id}/assignment",
            new SetMissionAssignmentRequest(driver.Id, vehicle.Id, planned.RowVersion));
        var assigned = await assignmentResponse.Content.ReadFromJsonAsync<MissionDetailResponse>();
        Assert.Equal(HttpStatusCode.OK, assignmentResponse.StatusCode);

        var assignedStatusResponse = await client.PostAsJsonAsync(
            $"/api/v1/dispatch/missions/{assigned!.Id}/status",
            new TransitionMissionStatusRequest(MissionStatus.Assigned, assigned.RowVersion));
        var progressed = await assignedStatusResponse.Content.ReadFromJsonAsync<MissionDetailResponse>();
        Assert.Equal(HttpStatusCode.OK, assignedStatusResponse.StatusCode);
        return progressed!;
    }

    private static async Task<MissionDetailResponse> CreateMissionAsync(HttpClient client, string reference, string title)
    {
        var start = DateTimeOffset.UtcNow.AddHours(3);
        var response = await client.PostAsJsonAsync(
            "/api/v1/dispatch/missions",
            new CreateMissionRequest(
                reference,
                title,
                start,
                start.AddHours(2),
                [
                    new MissionStopRequest(1, "Depot", "1 Dispatch Way", start.AddMinutes(20)),
                    new MissionStopRequest(2, "Client", "2 Fleet Street", start.AddMinutes(90))
                ]));
        var mission = await response.Content.ReadFromJsonAsync<MissionDetailResponse>();
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return mission!;
    }

    private async Task ResetDatabaseAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FleetOpsDbContext>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();
        await FleetOpsSeedData.EnsureSeededAsync(dbContext, roleManager, userManager, CancellationToken.None);
    }

    private sealed record DriverAssignmentCandidate(Guid Id, string FullName, string LicenseNumber, string? PhoneNumber, bool IsActive, long RowVersion);
    private sealed record VehicleAssignmentCandidate(Guid Id, string RegistrationNumber, string DisplayName, bool IsActive, long RowVersion);
}
