using System.Net;
using System.Net.Http.Json;
using FleetOps.Api.Alerts;
using FleetOps.Api.Dispatch;
using FleetOps.Api.DriverApp;
using FleetOps.Api.Fleet;
using FleetOps.Api.Operations;
using FleetOps.Core.Modules.Dispatch;
using FleetOps.Core.Modules.Operations;
using FleetOps.Infrastructure.Identity;
using FleetOps.Infrastructure.Persistence;
using FleetOps.UnitTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FleetOps.UnitTests;

public sealed class OperationsCenterIntegrationTests(FleetOpsApiFactory factory) : IClassFixture<FleetOpsApiFactory>
{
    [Fact]
    public async Task QueueUnifiesDelayCriticalDefectAndBlockedSyncForTheTenant()
    {
        await ResetDatabaseAsync();

        using var operatorClient = factory.CreateClient();
        var operatorLogin = await operatorClient.LoginAsync("operator@northwind.local", "Operator123!");
        operatorClient.SetBearer(operatorLogin.AccessToken);

        var mission = await ProgressMissionToAssignedAsync(
            operatorClient,
            await CreateMissionAsync(operatorClient, "NW-M-OPS-QUEUE", "Operations queue mission"));

        var delayResponse = await operatorClient.PostAsJsonAsync(
            $"/api/v1/dispatch/missions/{mission.Id}/delay-simulation",
            new SimulateMissionDelayRequest(45, mission.RowVersion));
        var delayedMission = await delayResponse.Content.ReadFromJsonAsync<MissionDetailResponse>();
        Assert.Equal(HttpStatusCode.OK, delayResponse.StatusCode);

        using var driverClient = factory.CreateClient();
        var driverLogin = await driverClient.LoginAsync("driver@northwind.local", "Driver123!");
        driverClient.SetBearer(driverLogin.AccessToken);

        var inspectionResponse = await driverClient.PostAsJsonAsync(
            $"/api/v1/driver/missions/{mission.Id}/inspection",
            new SubmitPreDepartureInspectionRequest(
                "inspection-ops-001",
                DateTimeOffset.UtcNow,
                "Vehicle blocked",
                [
                    new InspectionItemResultRequest(1, "brakes", "Brakes and steering", false, DefectSeverity.Critical, "Hydraulic issue", null),
                    new InspectionItemResultRequest(2, "lights", "Lights and signals", true, DefectSeverity.None, null, null),
                    new InspectionItemResultRequest(3, "cargo", "Cargo secured", true, DefectSeverity.None, null, null),
                ]));
        Assert.Equal(HttpStatusCode.OK, inspectionResponse.StatusCode);

        var staleSyncResponse = await driverClient.PostAsJsonAsync(
            $"/api/v1/driver/missions/{mission.Id}/commands",
            new SyncMissionCommandRequest(
                "sync-ops-stale-001",
                DriverMissionCommandAction.Start,
                delayedMission!.RowVersion - 1,
                DateTimeOffset.UtcNow));
        Assert.Equal(HttpStatusCode.Conflict, staleSyncResponse.StatusCode);

        var queue = await operatorClient.GetFromJsonAsync<OperationsExceptionQueueResponse>("/api/v1/operations/exceptions");
        Assert.NotNull(queue);
        Assert.Contains(queue!.Items, x => x.SourceType == "MissionDelay" && x.Links.MissionId == mission.Id);
        Assert.Contains(queue.Items, x => x.SourceType == "CriticalDefect" && x.Links.MissionId == mission.Id);
        Assert.Contains(queue.Items, x => x.SourceType == "DriverSync" && x.Links.MissionId == mission.Id);
    }

    [Fact]
    public async Task QueueActionsAreTenantSafeAndConcurrencyChecked()
    {
        await ResetDatabaseAsync();

        using var northClient = factory.CreateClient();
        var northLogin = await northClient.LoginAsync("admin@northwind.local", "Admin123!");
        northClient.SetBearer(northLogin.AccessToken);

        var vehicles = await northClient.GetFromJsonAsync<List<VehicleResponse>>("/api/v1/fleet/vehicles");
        var targetVehicle = Assert.Single(vehicles!, x => x.RegistrationNumber == "NW-100");
        await northClient.PostAsJsonAsync(
            $"/api/v1/fleet/vehicles/{targetVehicle.Id}/documents",
            new CreateComplianceDocumentRequest(
                "Insurance",
                "OPS-ALERT-001",
                new DateTimeOffset(2026, 7, 18, 10, 0, 0, TimeSpan.Zero),
                "Expires tomorrow"));
        await northClient.PostAsync("/api/v1/alerts/scan", null);

        var queue = await northClient.GetFromJsonAsync<OperationsExceptionQueueResponse>("/api/v1/operations/exceptions?sourceType=Alert");
        var alertItem = Assert.Single(
            queue!.Items,
            x => x.SourceType == "Alert" && x.Message.Contains("Insurance", StringComparison.OrdinalIgnoreCase));

        var assignResponse = await northClient.PostAsJsonAsync(
            $"/api/v1/operations/exceptions/{alertItem.Id}/assign",
            new OperationsAssignRequest(northLogin.User.UserId, alertItem.ConcurrencyToken));
        Assert.Equal(HttpStatusCode.OK, assignResponse.StatusCode);

        var staleResolve = await northClient.PostAsJsonAsync(
            $"/api/v1/operations/exceptions/{alertItem.Id}/resolve",
            new OperationsResolveRequest(alertItem.ConcurrencyToken, "Handled after insurance renewal"));
        Assert.Equal(HttpStatusCode.Conflict, staleResolve.StatusCode);

        using var southClient = factory.CreateClient();
        var southLogin = await southClient.LoginAsync("admin@southridge.local", "Admin123!");
        southClient.SetBearer(southLogin.AccessToken);
        var notFound = await southClient.PostAsJsonAsync(
            $"/api/v1/operations/exceptions/{alertItem.Id}/acknowledge",
            new OperationsActionRequest(alertItem.ConcurrencyToken));
        Assert.Equal(HttpStatusCode.NotFound, notFound.StatusCode);
    }

    [Fact]
    public async Task OperatorsCanPersistAndReuseSavedViews()
    {
        await ResetDatabaseAsync();

        using var client = factory.CreateClient();
        var login = await client.LoginAsync("operator@northwind.local", "Operator123!");
        client.SetBearer(login.AccessToken);

        var createResponse = await client.PostAsJsonAsync(
            "/api/v1/operations/saved-views",
            new CreateOperationsSavedViewRequest(
                "Critical delays",
                true,
                new OperationsSavedViewFilterRequest("delay", "MissionDelay", "Critical", "Open", null, false)));
        var created = await createResponse.Content.ReadFromJsonAsync<OperationsSavedViewResponse>();

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(created);

        var list = await client.GetFromJsonAsync<List<OperationsSavedViewResponse>>("/api/v1/operations/saved-views");
        Assert.Contains(list!, x => x.Name == "Critical delays" && x.IsShared);
    }

    private static async Task<MissionDetailResponse> ProgressMissionToAssignedAsync(HttpClient client, MissionDetailResponse mission)
    {
        var plannedResponse = await client.PostAsJsonAsync(
            $"/api/v1/dispatch/missions/{mission.Id}/status",
            new TransitionMissionStatusRequest(MissionStatus.Planned, mission.RowVersion));
        var planned = await plannedResponse.Content.ReadFromJsonAsync<MissionDetailResponse>();
        Assert.Equal(HttpStatusCode.OK, plannedResponse.StatusCode);

        var drivers = await client.GetFromJsonAsync<List<DriverCandidate>>("/api/v1/fleet/drivers");
        var vehicles = await client.GetFromJsonAsync<List<VehicleCandidate>>("/api/v1/fleet/vehicles");
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
        await FleetOpsSeedData.EnsureSeededAsync(
            dbContext,
            roleManager,
            userManager,
            new BootstrapOptions { SeedDemoData = true },
            CancellationToken.None);
    }

    private sealed record DriverCandidate(Guid Id, string FullName, string LicenseNumber, string? PhoneNumber, bool IsActive, long RowVersion);
    private sealed record VehicleCandidate(Guid Id, string RegistrationNumber, string DisplayName, bool IsActive, long RowVersion);
}
