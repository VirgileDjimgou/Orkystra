using System.Net;
using System.Net.Http.Json;
using FleetOps.Api.Auth;
using FleetOps.Api.Fleet;
using FleetOps.UnitTests.Infrastructure;
using Xunit;

namespace FleetOps.UnitTests;

public sealed class SimulationSeedIntegrationTests(FleetOpsApiFactory factory) : IClassFixture<FleetOpsApiFactory>
{
    public static TheoryData<string, string, string, string> RoleAccounts => new()
    {
        { "admin@northwind.local", "Admin123!", "Admin", "Northwind Logistics" },
        { "operator@northwind.local", "Operator123!", "Operator", "Northwind Logistics" },
        { "driver@northwind.local", "Driver123!", "Driver", "Northwind Logistics" },
        { "admin@southridge.local", "Admin123!", "Admin", "Southridge Transport" },
        { "operator@southridge.local", "Operator123!", "Operator", "Southridge Transport" },
        { "driver@southridge.local", "Driver123!", "Driver", "Southridge Transport" },
        { "admin@westland.local", "Admin123!", "Admin", "Westland Field Services" },
        { "operator@westland.local", "Operator123!", "Operator", "Westland Field Services" },
        { "driver@westland.local", "Driver123!", "Driver", "Westland Field Services" },
    };

    [Theory]
    [MemberData(nameof(RoleAccounts))]
    public async Task DemoSeedProvidesEveryRoleForEverySimulationTenant(
        string email,
        string password,
        string expectedRole,
        string expectedOrganization)
    {
        using var client = factory.CreateClient();

        var login = await client.LoginAsync(email, password);

        Assert.Contains(expectedRole, login.User.Roles);
        Assert.Equal(expectedOrganization, login.User.OrganizationName);
        Assert.NotEqual(Guid.Empty, login.User.UserId);
        Assert.Equal(expectedRole == "Driver", login.User.DriverId.HasValue);
    }

    [Fact]
    public async Task WestlandInventoryIsTenantScopedFromTheAuthenticatedIdentity()
    {
        using var west = factory.CreateClient();
        west.SetBearer((await west.LoginAsync("operator@westland.local", "Operator123!")).AccessToken);
        var westVehicles = await west.GetFromJsonAsync<List<VehicleResponse>>("/api/v1/fleet/vehicles");

        using var north = factory.CreateClient();
        north.SetBearer((await north.LoginAsync("operator@northwind.local", "Operator123!")).AccessToken);
        var foreignVehicle = await north.GetAsync($"/api/v1/fleet/vehicles/{westVehicles![0].Id}");

        Assert.Equal(3, westVehicles.Count);
        Assert.All(westVehicles, vehicle => Assert.StartsWith("WF-", vehicle.RegistrationNumber));
        Assert.Equal(HttpStatusCode.NotFound, foreignVehicle.StatusCode);
    }
}
