using System.Net;
using System.Net.Http.Json;
using FleetOps.Api.Demo;
using FleetOps.Api.Dispatch;
using FleetOps.Core.Modules.Demo;
using FleetOps.Infrastructure.Identity;
using FleetOps.Infrastructure.Persistence;
using FleetOps.UnitTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FleetOps.UnitTests;

public sealed class AgentActivityIntegrationTests(FleetOpsApiFactory factory) : IClassFixture<FleetOpsApiFactory>
{
    [Fact]
    public async Task ActivityIsIdempotentAuditedAndTenantScoped()
    {
        await ResetDatabaseAsync();
        using var north = factory.CreateClient();
        var northLogin = await north.LoginAsync("operator@northwind.local", "Operator123!");
        north.SetBearer(northLogin.AccessToken);
        var drivers = await north.GetFromJsonAsync<List<DriverCandidate>>("/api/v1/fleet/drivers");
        var vehicles = await north.GetFromJsonAsync<List<VehicleCandidate>>("/api/v1/fleet/vehicles");
        var driver = Assert.Single(drivers!, item => item.LicenseNumber == "NW-DL-001");
        var vehicle = Assert.Single(vehicles!, item => item.RegistrationNumber == "NW-100");
        var start = DateTimeOffset.UtcNow.AddHours(1);
        using var created = await north.PostAsJsonAsync("/api/v1/dispatch/missions", new CreateMissionRequest(
            "AGENT-ACTIVITY-28", "Virtual driver activity", start, start.AddHours(1),
            [new MissionStopRequest(1, "Synthetic stop", "1 Demo Way", start.AddMinutes(30))]));
        created.EnsureSuccessStatusCode();
        var mission = (await created.Content.ReadFromJsonAsync<MissionDetailResponse>())!;
        var request = new RecordAgentActivityRequest(
            Guid.NewGuid(), driver.Id, vehicle.Id, mission.Id, mission.Stops[0].Id, 1,
            VirtualDriverState.Assigned, "assigned-pre-trip", VirtualDriverAction.SubmitInspection,
            "accepted", "Typed workflow accepted.", DateTimeOffset.UtcNow);

        using var first = await north.PostAsJsonAsync("/api/v1/demo/agent-activities", request);
        using var duplicate = await north.PostAsJsonAsync("/api/v1/demo/agent-activities", request);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
        var northItems = await north.GetFromJsonAsync<List<AgentActivityResponse>>("/api/v1/demo/agent-activities");
        Assert.Single(northItems!);

        using var south = factory.CreateClient();
        var southLogin = await south.LoginAsync("operator@southridge.local", "Operator123!");
        south.SetBearer(southLogin.AccessToken);
        Assert.Empty((await south.GetFromJsonAsync<List<AgentActivityResponse>>("/api/v1/demo/agent-activities"))!);
        using var crossTenant = await south.PostAsJsonAsync("/api/v1/demo/agent-activities", request with { AgentId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.NotFound, crossTenant.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FleetOpsDbContext>();
        var northwindId = await db.Organizations.Where(item => item.Slug == "northwind").Select(item => item.Id).SingleAsync();
        Assert.Single(await db.AgentActivities.Where(item => item.OrganizationId == northwindId).ToListAsync());
        Assert.Contains(await db.AuditLogs.Where(item => item.OrganizationId == northwindId).ToListAsync(), item => item.ActionType == "demo.agent_action");
    }

    [Fact]
    public async Task AnonymousActivityAccessIsRejected()
    {
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/demo/agent-activities")).StatusCode);
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

    private sealed record DriverCandidate(Guid Id, string LicenseNumber);
    private sealed record VehicleCandidate(Guid Id, string RegistrationNumber);
}
