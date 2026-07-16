using System.Net;
using System.Net.Http.Json;
using FleetOps.Api.Alerts;
using FleetOps.Api.Fleet;
using FleetOps.UnitTests.Infrastructure;
using Xunit;

namespace FleetOps.UnitTests;

public sealed class AlertIntegrationTests(FleetOpsApiFactory factory) : IClassFixture<FleetOpsApiFactory>
{
    [Fact]
    public async Task OperatorCanAcknowledgeAlertButDriverCannot()
    {
        using var adminClient = factory.CreateClient();
        var adminLogin = await adminClient.LoginAsync("admin@northwind.local", "Admin123!");
        adminClient.SetBearer(adminLogin.AccessToken);

        var vehicle = await (await adminClient.GetAsync("/api/v1/fleet/vehicles"))
            .Content.ReadFromJsonAsync<List<VehicleResponse>>();
        var targetVehicle = Assert.Single(vehicle!, x => x.RegistrationNumber == "NW-100");

        var documentResponse = await adminClient.PostAsJsonAsync(
            $"/api/v1/fleet/vehicles/{targetVehicle.Id}/documents",
            new CreateComplianceDocumentRequest(
                "Insurance",
                "POL-001",
                new DateTimeOffset(2026, 7, 18, 10, 0, 0, TimeSpan.Zero),
                "Expires soon"));
        Assert.Equal(HttpStatusCode.Created, documentResponse.StatusCode);

        await adminClient.PostAsync("/api/v1/alerts/scan", null);
        var alerts = await adminClient.GetFromJsonAsync<List<AlertListItemResponse>>("/api/v1/alerts");
        var alert = Assert.Single(alerts!, x => x.RuleType == Core.Modules.Alerts.AlertRuleType.VehicleDocumentExpiry);

        using var operatorClient = factory.CreateClient();
        var operatorLogin = await operatorClient.LoginAsync("operator@northwind.local", "Operator123!");
        operatorClient.SetBearer(operatorLogin.AccessToken);
        var acknowledge = await operatorClient.PostAsJsonAsync(
            $"/api/v1/alerts/{alert.Id}/acknowledge",
            new AcknowledgeAlertRequest(alert.RowVersion));

        Assert.Equal(HttpStatusCode.OK, acknowledge.StatusCode);

        using var driverClient = factory.CreateClient();
        var driverLogin = await driverClient.LoginAsync("driver@northwind.local", "Driver123!");
        driverClient.SetBearer(driverLogin.AccessToken);
        var forbidden = await driverClient.PostAsJsonAsync(
            $"/api/v1/alerts/{alert.Id}/acknowledge",
            new AcknowledgeAlertRequest(alert.RowVersion + 1));

        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task AlertScanDoesNotDuplicateAlerts()
    {
        using var client = factory.CreateClient();
        var login = await client.LoginAsync("admin@northwind.local", "Admin123!");
        client.SetBearer(login.AccessToken);

        var driver = await (await client.GetAsync("/api/v1/fleet/drivers"))
            .Content.ReadFromJsonAsync<List<DriverResponse>>();
        var targetDriver = Assert.Single(driver!, x => x.LicenseNumber == "NW-DL-001");

        await client.PostAsJsonAsync(
            $"/api/v1/fleet/drivers/{targetDriver.Id}/documents",
            new CreateComplianceDocumentRequest(
                "License",
                "NW-LIC-01",
                new DateTimeOffset(2026, 7, 18, 9, 0, 0, TimeSpan.Zero),
                null));

        var firstScan = await client.PostAsync("/api/v1/alerts/scan", null);
        var secondScan = await client.PostAsync("/api/v1/alerts/scan", null);
        var firstResult = await firstScan.Content.ReadFromJsonAsync<ScanAlertsResponse>();
        var secondResult = await secondScan.Content.ReadFromJsonAsync<ScanAlertsResponse>();
        var alerts = await client.GetFromJsonAsync<List<AlertListItemResponse>>("/api/v1/alerts");

        Assert.Equal(HttpStatusCode.OK, firstScan.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondScan.StatusCode);
        Assert.NotNull(firstResult);
        Assert.NotNull(secondResult);
        Assert.Equal(0, secondResult!.CreatedAlerts);
        Assert.Single(alerts!, x => x.RuleType == Core.Modules.Alerts.AlertRuleType.DriverDocumentExpiry);
    }

    [Fact]
    public async Task AlertsRemainTenantIsolated()
    {
        using var northClient = factory.CreateClient();
        var northLogin = await northClient.LoginAsync("admin@northwind.local", "Admin123!");
        northClient.SetBearer(northLogin.AccessToken);
        var vehicles = await northClient.GetFromJsonAsync<List<VehicleResponse>>("/api/v1/fleet/vehicles");
        var targetVehicle = Assert.Single(vehicles!, x => x.RegistrationNumber == "NW-101");

        await northClient.PostAsJsonAsync(
            $"/api/v1/fleet/vehicles/{targetVehicle.Id}/documents",
            new CreateComplianceDocumentRequest(
                "Inspection",
                "INS-200",
                new DateTimeOffset(2026, 7, 19, 9, 0, 0, TimeSpan.Zero),
                null));
        await northClient.PostAsync("/api/v1/alerts/scan", null);
        var northAlerts = await northClient.GetFromJsonAsync<List<AlertListItemResponse>>("/api/v1/alerts");
        var northAlert = Assert.Single(
            northAlerts!,
            x => x.RuleType == Core.Modules.Alerts.AlertRuleType.VehicleDocumentExpiry
                && x.TargetEntityId == targetVehicle.Id);

        using var southClient = factory.CreateClient();
        var southLogin = await southClient.LoginAsync("admin@southridge.local", "Admin123!");
        southClient.SetBearer(southLogin.AccessToken);

        var southAlerts = await southClient.GetFromJsonAsync<List<AlertListItemResponse>>("/api/v1/alerts");
        var crossAcknowledge = await southClient.PostAsJsonAsync(
            $"/api/v1/alerts/{northAlert.Id}/acknowledge",
            new AcknowledgeAlertRequest(northAlert.RowVersion));

        Assert.DoesNotContain(southAlerts!, x => x.Id == northAlert.Id);
        Assert.Equal(HttpStatusCode.NotFound, crossAcknowledge.StatusCode);
    }
}
