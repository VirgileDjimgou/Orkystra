using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FleetOps.Api.Fleet;
using FleetOps.Core.Modules.Identity;
using FleetOps.Infrastructure.Persistence;
using FleetOps.UnitTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FleetOps.UnitTests;

public sealed class FleetRegistryIntegrationTests(FleetOpsApiFactory factory) : IClassFixture<FleetOpsApiFactory>
{
    [Fact]
    public async Task AdminSeesOnlyTenantVehicles()
    {
        using var client = factory.CreateClient();
        var login = await client.LoginAsync("admin@northwind.local", "Admin123!");
        client.SetBearer(login.AccessToken);

        var vehicles = await client.GetFromJsonAsync<List<VehicleResponse>>("/api/v1/fleet/vehicles");

        Assert.NotNull(vehicles);
        Assert.NotEmpty(vehicles);
        Assert.All(vehicles!, v => Assert.StartsWith("NW-", v.RegistrationNumber, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SouthridgeCannotSeeNorthwindVehicles()
    {
        using var client = factory.CreateClient();
        var login = await client.LoginAsync("admin@southridge.local", "Admin123!");
        client.SetBearer(login.AccessToken);

        var vehicles = await client.GetFromJsonAsync<List<VehicleResponse>>("/api/v1/fleet/vehicles");

        Assert.NotNull(vehicles);
        Assert.All(vehicles!, v => Assert.StartsWith("SR-", v.RegistrationNumber, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(vehicles!, v => v.RegistrationNumber.Equals("NW-100", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task OperatorCanListButCannotCreateVehicles()
    {
        using var client = factory.CreateClient();
        var login = await client.LoginAsync("operator@northwind.local", "Operator123!");
        client.SetBearer(login.AccessToken);

        var list = await client.GetAsync("/api/v1/fleet/vehicles");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);

        var create = await client.PostAsJsonAsync("/api/v1/fleet/vehicles", new CreateVehicleRequest("NW-TEST", "Test van"));
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
    }

    [Fact]
    public async Task CreatingDuplicateRegistrationReturnsValidationProblem()
    {
        using var client = factory.CreateClient();
        var login = await client.LoginAsync("admin@northwind.local", "Admin123!");
        client.SetBearer(login.AccessToken);

        var duplicate = await client.PostAsJsonAsync("/api/v1/fleet/vehicles", new CreateVehicleRequest("NW-100", "Duplicate"));

        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
    }

    [Fact]
    public async Task CreatingDuplicateLicenseAndSerialReturnValidationProblems()
    {
        using var client = factory.CreateClient();
        var login = await client.LoginAsync("admin@northwind.local", "Admin123!");
        client.SetBearer(login.AccessToken);

        var duplicateDriver = await client.PostAsJsonAsync(
            "/api/v1/fleet/drivers",
            new CreateDriverRequest("Duplicate Driver", "NW-DL-001", null));
        var duplicateDevice = await client.PostAsJsonAsync(
            "/api/v1/fleet/devices",
            new CreateGpsDeviceRequest("NW-GPS-100", "Duplicate GPS"));

        Assert.Equal(HttpStatusCode.BadRequest, duplicateDriver.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, duplicateDevice.StatusCode);
    }

    [Fact]
    public async Task CreateVehicleWithBlankRegistrationReturnsValidationProblem()
    {
        using var client = factory.CreateClient();
        var login = await client.LoginAsync("admin@northwind.local", "Admin123!");
        client.SetBearer(login.AccessToken);

        var invalid = await client.PostAsJsonAsync("/api/v1/fleet/vehicles", new CreateVehicleRequest("   ", "Blank van"));

        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task UpdateVehicleStaleVersionReturns409()
    {
        using var client = factory.CreateClient();
        var login = await client.LoginAsync("admin@northwind.local", "Admin123!");
        client.SetBearer(login.AccessToken);

        var created = await client.PostAsJsonAsync("/api/v1/fleet/vehicles", new CreateVehicleRequest("NW-CONC", "Concurrent van"));
        var vehicle = await created.Content.ReadFromJsonAsync<VehicleResponse>();

        var firstUpdate = await client.PutAsJsonAsync(
            $"/api/v1/fleet/vehicles/{vehicle!.Id}",
            new UpdateVehicleRequest("Updated once", vehicle.RowVersion));
        Assert.Equal(HttpStatusCode.OK, firstUpdate.StatusCode);

        var staleUpdate = await client.PutAsJsonAsync(
            $"/api/v1/fleet/vehicles/{vehicle.Id}",
            new UpdateVehicleRequest("Stale update", vehicle.RowVersion));

        Assert.Equal(HttpStatusCode.Conflict, staleUpdate.StatusCode);
    }

    [Fact]
    public async Task DeactivateAndActivateVehicle()
    {
        using var client = factory.CreateClient();
        var login = await client.LoginAsync("admin@northwind.local", "Admin123!");
        client.SetBearer(login.AccessToken);

        var created = await client.PostAsJsonAsync("/api/v1/fleet/vehicles", new CreateVehicleRequest("NW-SWITCH", "Toggle van"));
        var vehicle = await created.Content.ReadFromJsonAsync<VehicleResponse>();

        var deactivate = await client.PostAsync($"/api/v1/fleet/vehicles/{vehicle!.Id}/deactivate", null);
        var deactivated = await deactivate.Content.ReadFromJsonAsync<VehicleResponse>();
        Assert.False(deactivated!.IsActive);

        var reactivate = await client.PostAsync($"/api/v1/fleet/vehicles/{vehicle.Id}/activate", null);
        var reactivated = await reactivate.Content.ReadFromJsonAsync<VehicleResponse>();
        Assert.True(reactivated!.IsActive);
    }

    [Fact]
    public async Task ImportVehiclesIdempotentUpsert()
    {
        using var client = factory.CreateClient();
        var login = await client.LoginAsync("admin@northwind.local", "Admin123!");
        client.SetBearer(login.AccessToken);

        var firstCsv = "registration,display\nNW-IMP-1,Van One\nNW-IMP-2,Van Two";
        var first = await PostCsvAsync(client, "/api/v1/fleet/vehicles/import", firstCsv);
        var firstSummary = await first.Content.ReadFromJsonAsync<ImportSummary>();
        Assert.Equal(2, firstSummary!.Created);

        var secondCsv = "registration,display\nNW-IMP-1,Van One Renamed\nNW-IMP-3,Van Three";
        var second = await PostCsvAsync(client, "/api/v1/fleet/vehicles/import", secondCsv);
        var secondSummary = await second.Content.ReadFromJsonAsync<ImportSummary>();

        Assert.Equal(1, secondSummary!.Created);
        Assert.Equal(1, secondSummary.Updated);

        var vehicles = await client.GetFromJsonAsync<List<VehicleResponse>>("/api/v1/fleet/vehicles");
        Assert.Contains(vehicles!, v => v.RegistrationNumber == "NW-IMP-1" && v.DisplayName == "Van One Renamed");
        Assert.Contains(vehicles!, v => v.RegistrationNumber == "NW-IMP-2" && v.DisplayName == "Van Two");
        Assert.Contains(vehicles!, v => v.RegistrationNumber == "NW-IMP-3" && v.DisplayName == "Van Three");
    }

    [Fact]
    public async Task ActiveDeviceCanOnlyHaveOneActiveAssignment()
    {
        using var client = factory.CreateClient();
        var login = await client.LoginAsync("admin@northwind.local", "Admin123!");
        client.SetBearer(login.AccessToken);

        var vehicleResponse = await client.PostAsJsonAsync("/api/v1/fleet/vehicles", new CreateVehicleRequest("NW-DEV-VEH", "Dev vehicle"));
        var vehicle = await vehicleResponse.Content.ReadFromJsonAsync<VehicleResponse>();
        var deviceResponse = await client.PostAsJsonAsync("/api/v1/fleet/devices", new CreateGpsDeviceRequest("NW-DEV-001", "Dev device"));
        var device = await deviceResponse.Content.ReadFromJsonAsync<GpsDeviceResponse>();

        var firstAssignment = await client.PostAsJsonAsync(
            $"/api/v1/fleet/devices/{device!.Id}/assignments/active",
            new AssignDeviceRequest(vehicle!.Id));
        Assert.Equal(HttpStatusCode.Created, firstAssignment.StatusCode);

        var secondAssignment = await client.PostAsJsonAsync(
            $"/api/v1/fleet/devices/{device.Id}/assignments/active",
            new AssignDeviceRequest(vehicle.Id));

        Assert.Equal(HttpStatusCode.Conflict, secondAssignment.StatusCode);

        var close = await client.PostAsync($"/api/v1/fleet/devices/{device.Id}/assignments/active/close", null);
        Assert.Equal(HttpStatusCode.OK, close.StatusCode);

        var reassign = await client.PostAsJsonAsync(
            $"/api/v1/fleet/devices/{device.Id}/assignments/active",
            new AssignDeviceRequest(vehicle.Id));
        Assert.Equal(HttpStatusCode.Created, reassign.StatusCode);
    }

    [Fact]
    public async Task OperatorCanAssignButCannotCreateDevices()
    {
        using var adminClient = factory.CreateClient();
        var adminLogin = await adminClient.LoginAsync("admin@northwind.local", "Admin123!");
        adminClient.SetBearer(adminLogin.AccessToken);

        var vehicle = await (await adminClient.PostAsJsonAsync(
            "/api/v1/fleet/vehicles",
            new CreateVehicleRequest("NW-OP-VEH", "Operator vehicle"))).Content.ReadFromJsonAsync<VehicleResponse>();
        var device = await (await adminClient.PostAsJsonAsync(
            "/api/v1/fleet/devices",
            new CreateGpsDeviceRequest("NW-OP-DEV", "Operator device"))).Content.ReadFromJsonAsync<GpsDeviceResponse>();

        using var operatorClient = factory.CreateClient();
        var operatorLogin = await operatorClient.LoginAsync("operator@northwind.local", "Operator123!");
        operatorClient.SetBearer(operatorLogin.AccessToken);

        var create = await operatorClient.PostAsJsonAsync(
            "/api/v1/fleet/devices",
            new CreateGpsDeviceRequest("NW-OP-FORBID", "Forbidden"));
        var assign = await operatorClient.PostAsJsonAsync(
            $"/api/v1/fleet/devices/{device!.Id}/assignments/active",
            new AssignDeviceRequest(vehicle!.Id));

        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        Assert.Equal(HttpStatusCode.Created, assign.StatusCode);
    }

    [Fact]
    public async Task DevicesCrossTenantIsolation()
    {
        using var northClient = factory.CreateClient();
        var northLogin = await northClient.LoginAsync("admin@northwind.local", "Admin123!");
        northClient.SetBearer(northLogin.AccessToken);

        var northDevice = await (await northClient.PostAsJsonAsync("/api/v1/fleet/devices", new CreateGpsDeviceRequest("NW-DEV-X", "North"))).Content.ReadFromJsonAsync<GpsDeviceResponse>();

        using var southClient = factory.CreateClient();
        var southLogin = await southClient.LoginAsync("admin@southridge.local", "Admin123!");
        southClient.SetBearer(southLogin.AccessToken);

        var crossFetch = await southClient.GetAsync($"/api/v1/fleet/devices/{northDevice!.Id}");
        Assert.Equal(HttpStatusCode.NotFound, crossFetch.StatusCode);

        var crossClose = await southClient.PostAsync($"/api/v1/fleet/devices/{northDevice.Id}/assignments/active/close", null);
        Assert.Equal(HttpStatusCode.NotFound, crossClose.StatusCode);
    }

    [Fact]
    public async Task InactiveDeviceCannotBeAssigned()
    {
        using var client = factory.CreateClient();
        var login = await client.LoginAsync("admin@northwind.local", "Admin123!");
        client.SetBearer(login.AccessToken);

        var vehicle = await (await client.PostAsJsonAsync("/api/v1/fleet/vehicles", new CreateVehicleRequest("NW-INACT-VEH", "Vehicle"))).Content.ReadFromJsonAsync<VehicleResponse>();
        var device = await (await client.PostAsJsonAsync("/api/v1/fleet/devices", new CreateGpsDeviceRequest("NW-INACT-DEV", "Inactive"))).Content.ReadFromJsonAsync<GpsDeviceResponse>();

        await client.PostAsync($"/api/v1/fleet/devices/{device!.Id}/deactivate", null);

        var assignment = await client.PostAsJsonAsync(
            $"/api/v1/fleet/devices/{device.Id}/assignments/active",
            new AssignDeviceRequest(vehicle!.Id));

        Assert.Equal(HttpStatusCode.BadRequest, assignment.StatusCode);
    }

    [Fact]
    public async Task FleetOperationsAreAudited()
    {
        using var client = factory.CreateClient();
        var login = await client.LoginAsync("admin@northwind.local", "Admin123!");
        client.SetBearer(login.AccessToken);

        await client.PostAsJsonAsync("/api/v1/fleet/vehicles", new CreateVehicleRequest("NW-AUDIT", "Audited van"));
        await client.PostAsJsonAsync("/api/v1/fleet/drivers", new CreateDriverRequest("Audited Driver", "NW-AUDIT-LIC", null));

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FleetOpsDbContext>();
        var auditEntries = dbContext.AuditLogs.Where(x => x.OrganizationId == dbContext.Organizations.Single(o => o.Slug == "northwind").Id).ToList();

        Assert.Contains(auditEntries, x => x.ActionType == "fleet.vehicle_created");
        Assert.Contains(auditEntries, x => x.ActionType == "fleet.driver_created");
    }

    [Fact]
    public async Task ImportDriversIdempotentUpsert()
    {
        using var client = factory.CreateClient();
        var login = await client.LoginAsync("admin@northwind.local", "Admin123!");
        client.SetBearer(login.AccessToken);

        var firstCsv = "name,license,phone\nAnna,NW-DL-IMP-1,+1-555\nBob,NW-DL-IMP-2,";
        await PostCsvAsync(client, "/api/v1/fleet/drivers/import", firstCsv);

        var secondCsv = "name,license,phone\nAnna Renamed,NW-DL-IMP-1,+1-555-99\nCara,NW-DL-IMP-3,";
        await PostCsvAsync(client, "/api/v1/fleet/drivers/import", secondCsv);

        var drivers = await client.GetFromJsonAsync<List<DriverResponse>>("/api/v1/fleet/drivers");
        var anna = Assert.Single(drivers!, d => d.LicenseNumber == "NW-DL-IMP-1");
        Assert.Equal("Anna Renamed", anna.FullName);
        Assert.Equal("+1-555-99", anna.PhoneNumber);
        Assert.Contains(drivers!, d => d.LicenseNumber == "NW-DL-IMP-2");
        Assert.Contains(drivers!, d => d.LicenseNumber == "NW-DL-IMP-3");
    }

    [Fact]
    public async Task ImportDevicesIdempotentUpsert()
    {
        using var client = factory.CreateClient();
        var login = await client.LoginAsync("admin@northwind.local", "Admin123!");
        client.SetBearer(login.AccessToken);

        var firstCsv = "serial,display\nNW-GPS-IMP-1,Device One\nNW-GPS-IMP-2,Device Two";
        var first = await PostCsvAsync(client, "/api/v1/fleet/devices/import", firstCsv);
        var firstSummary = await first.Content.ReadFromJsonAsync<ImportSummary>();
        Assert.Equal(2, firstSummary!.Created);

        var secondCsv = "serial,display\nNW-GPS-IMP-1,Device One Renamed\nNW-GPS-IMP-3,Device Three";
        var second = await PostCsvAsync(client, "/api/v1/fleet/devices/import", secondCsv);
        var secondSummary = await second.Content.ReadFromJsonAsync<ImportSummary>();

        Assert.Equal(1, secondSummary!.Created);
        Assert.Equal(1, secondSummary.Updated);

        var devices = await client.GetFromJsonAsync<List<GpsDeviceResponse>>("/api/v1/fleet/devices");
        Assert.Contains(devices!, d => d.SerialNumber == "NW-GPS-IMP-1" && d.DisplayName == "Device One Renamed");
        Assert.Contains(devices!, d => d.SerialNumber == "NW-GPS-IMP-2");
        Assert.Contains(devices!, d => d.SerialNumber == "NW-GPS-IMP-3");
    }

    [Fact]
    public async Task MalformedCsvReturnsValidationProblem()
    {
        using var client = factory.CreateClient();
        var login = await client.LoginAsync("admin@northwind.local", "Admin123!");
        client.SetBearer(login.AccessToken);

        var response = await PostCsvAsync(client, "/api/v1/fleet/vehicles/import", "NW-ONLY-ONE-COLUMN");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> PostCsvAsync(HttpClient client, string requestUri, string csv)
    {
        using var content = new StringContent(csv);
        content.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        return await client.PostAsync(requestUri, content);
    }
}
