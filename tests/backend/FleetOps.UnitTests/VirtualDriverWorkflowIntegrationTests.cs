extern alias worker;

using System.Net.Http.Json;
using FleetOps.Api.Dispatch;
using FleetOps.Core.Modules.Demo;
using FleetOps.Core.Modules.Dispatch;
using FleetOps.Infrastructure.Identity;
using FleetOps.Infrastructure.Persistence;
using FleetOps.UnitTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;
using DemoEngineOptions = worker::FleetOps.Worker.Demo.DemoEngineOptions;
using HttpVirtualDriverTools = worker::FleetOps.Worker.Demo.HttpVirtualDriverTools;

namespace FleetOps.UnitTests;

public sealed class VirtualDriverWorkflowIntegrationTests(FleetOpsApiFactory factory) : IClassFixture<FleetOpsApiFactory>
{
    [Fact]
    public async Task AgentCompletesRealInspectionMissionProofAndCompletionWorkflows()
    {
        await ResetDatabaseAsync();
        using var operatorClient = factory.CreateClient();
        var operatorLogin = await operatorClient.LoginAsync("operator@northwind.local", "Operator123!");
        operatorClient.SetBearer(operatorLogin.AccessToken);
        var mission = await CreateAssignedMissionAsync(operatorClient);
        using var driverClient = factory.CreateClient();
        var driverLogin = await driverClient.LoginAsync("driver@northwind.local", "Driver123!");
        var options = Options.Create(new DemoEngineOptions
        {
            ApiBaseUrl = "http://localhost",
            ApiAccessToken = operatorLogin.AccessToken,
            DriverAccessToken = driverLogin.AccessToken
        });
        var tools = new HttpVirtualDriverTools(new Factory(() => factory.CreateClient()), options);
        var identity = new VirtualDriverIdentity(Guid.NewGuid(), mission.OrganizationId, mission.DriverId, mission.VehicleId, mission.Id, mission.StopId);
        var sink = new Sink();
        var agent = new VirtualDriverAgent(identity, new DeterministicAgentDecisionProvider(), tools, sink);

        for (var tick = 1; tick <= 6; tick++)
            await agent.StepAsync(DemoScenarioKind.NormalShift, tick, DateTimeOffset.UtcNow.AddSeconds(tick), default);

        Assert.True(agent.State == VirtualDriverState.Completed, string.Join(" | ", sink.Activities.Select(activity => $"{activity.Action}:{activity.ResultCode}:{activity.ResultMessage}")));
        var completed = await operatorClient.GetFromJsonAsync<MissionDetailResponse>($"/api/v1/dispatch/missions/{mission.Id}");
        Assert.Equal(MissionStatus.Completed, completed!.Status);
        Assert.NotNull(completed.LatestInspection);
        Assert.Single(completed.DeliveryProofs);
    }

    private async Task<AssignedMission> CreateAssignedMissionAsync(HttpClient client)
    {
        var start = DateTimeOffset.UtcNow.AddHours(1);
        using var create = await client.PostAsJsonAsync("/api/v1/dispatch/missions", new CreateMissionRequest(
            "VIRTUAL-DRIVER-28", "Autonomous synthetic delivery", start, start.AddHours(1),
            [new MissionStopRequest(1, "Synthetic recipient", "28 Agent Way", start.AddMinutes(30))]));
        create.EnsureSuccessStatusCode();
        var draft = (await create.Content.ReadFromJsonAsync<MissionDetailResponse>())!;
        using var plannedResponse = await client.PostAsJsonAsync($"/api/v1/dispatch/missions/{draft.Id}/status", new TransitionMissionStatusRequest(MissionStatus.Planned, draft.RowVersion));
        plannedResponse.EnsureSuccessStatusCode();
        var planned = (await plannedResponse.Content.ReadFromJsonAsync<MissionDetailResponse>())!;
        var drivers = (await client.GetFromJsonAsync<List<DriverCandidate>>("/api/v1/fleet/drivers"))!;
        var vehicles = (await client.GetFromJsonAsync<List<VehicleCandidate>>("/api/v1/fleet/vehicles"))!;
        var driver = Assert.Single(drivers, item => item.LicenseNumber == "NW-DL-001");
        var vehicle = Assert.Single(vehicles, item => item.RegistrationNumber == "NW-100");
        using var assignResponse = await client.PutAsJsonAsync($"/api/v1/dispatch/missions/{draft.Id}/assignment", new SetMissionAssignmentRequest(driver.Id, vehicle.Id, planned.RowVersion));
        assignResponse.EnsureSuccessStatusCode();
        var assigned = (await assignResponse.Content.ReadFromJsonAsync<MissionDetailResponse>())!;
        using var statusResponse = await client.PostAsJsonAsync($"/api/v1/dispatch/missions/{draft.Id}/status", new TransitionMissionStatusRequest(MissionStatus.Assigned, assigned.RowVersion));
        statusResponse.EnsureSuccessStatusCode();
        var ready = (await statusResponse.Content.ReadFromJsonAsync<MissionDetailResponse>())!;
        using var scope = factory.Services.CreateScope();
        var organizationId = scope.ServiceProvider.GetRequiredService<FleetOpsDbContext>().Organizations.Single(item => item.Slug == "northwind").Id;
        return new(ready.Id, organizationId, driver.Id, vehicle.Id, ready.Stops[0].Id);
    }

    private async Task ResetDatabaseAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FleetOpsDbContext>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
        await FleetOpsSeedData.EnsureSeededAsync(db, roles, users, new BootstrapOptions { SeedDemoData = true }, default);
    }

    private sealed record AssignedMission(Guid Id, Guid OrganizationId, Guid DriverId, Guid VehicleId, Guid StopId);
    private sealed record DriverCandidate(Guid Id, string LicenseNumber);
    private sealed record VehicleCandidate(Guid Id, string RegistrationNumber);
    private sealed class Factory(Func<HttpClient> create) : IHttpClientFactory { public HttpClient CreateClient(string name) => create(); }
    private sealed class Sink : IAgentActivitySink
    {
        public List<AgentActivity> Activities { get; } = [];
        public Task RecordAsync(AgentActivity activity, CancellationToken cancellationToken) { Activities.Add(activity); return Task.CompletedTask; }
    }
}
