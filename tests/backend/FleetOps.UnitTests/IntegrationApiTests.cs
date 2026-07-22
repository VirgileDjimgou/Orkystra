using System.Net;
using System.Net.Http.Json;
using FleetOps.Api.Fleet;
using FleetOps.Api.Integrations;
using FleetOps.Api.Tracking;
using FleetOps.Core.Modules.Integrations;
using FleetOps.Infrastructure.Integrations;
using FleetOps.Infrastructure.Persistence;
using FleetOps.UnitTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FleetOps.UnitTests;

public sealed class IntegrationApiTests(FleetOpsApiFactory factory) : IClassFixture<FleetOpsApiFactory>
{
    [Fact]
    public async Task OpenApiDocumentExposesIntegrationRoutes()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");
        var document = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("/api/admin/integrations/api-keys", document, StringComparison.Ordinal);
        Assert.Contains("/api/v1/integrations/device/tracking/events", document, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PartnerApiKeyCanListVehiclesButCannotUseDeviceEndpoint()
    {
        using var adminClient = factory.CreateClient();
        var login = await adminClient.LoginAsync("admin@northwind.local", "Admin123!");
        adminClient.SetBearer(login.AccessToken);

        var createdKey = await (await adminClient.PostAsJsonAsync(
            "/api/admin/integrations/api-keys",
            new CreateApiClientCredentialRequest(
                "Northwind Partner",
                ApiClientCredentialType.Partner,
                [IntegrationScope.PartnerFleetRead])))
            .Content.ReadFromJsonAsync<CreatedApiClientCredentialResponse>();

        using var partnerClient = factory.CreateClient();
        partnerClient.DefaultRequestHeaders.Add("X-Api-Key", createdKey!.PlainTextSecret);

        var vehicles = await partnerClient.GetFromJsonAsync<List<PartnerVehicleExportResponse>>(
            "/api/v1/integrations/partner/fleet/vehicles");
        var forbidden = await partnerClient.PostAsJsonAsync(
            "/api/v1/integrations/device/tracking/events",
            new DeviceTelemetryIngestionRequest(
                Guid.NewGuid(),
                "NW-GPS-100",
                "evt-partner-1",
                DateTimeOffset.UtcNow,
                48.4,
                9.2,
                30,
                180));

        Assert.NotNull(vehicles);
        Assert.NotEmpty(vehicles);
        Assert.All(vehicles!, x => Assert.StartsWith("NW-", x.RegistrationNumber, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task DeviceApiKeyCanIngestTelemetryForItsTenant()
    {
        using var adminClient = factory.CreateClient();
        var login = await adminClient.LoginAsync("admin@northwind.local", "Admin123!");
        adminClient.SetBearer(login.AccessToken);

        await adminClient.PostAsync("/api/internal/v1/tracking/scenarios/northwind/reset", null);
        var createdKey = await (await adminClient.PostAsJsonAsync(
            "/api/admin/integrations/api-keys",
            new CreateApiClientCredentialRequest(
                "Northwind Device",
                ApiClientCredentialType.Device,
                [IntegrationScope.DeviceTrackingWrite])))
            .Content.ReadFromJsonAsync<CreatedApiClientCredentialResponse>();

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FleetOpsDbContext>();
        var vehicle = await dbContext.Vehicles.SingleAsync(x => x.RegistrationNumber == "NW-100");

        using var deviceClient = factory.CreateClient();
        deviceClient.DefaultRequestHeaders.Add("X-Api-Key", createdKey!.PlainTextSecret);

        var ingest = await deviceClient.PostAsJsonAsync(
            "/api/v1/integrations/device/tracking/events",
            new DeviceTelemetryIngestionRequest(
                vehicle.Id,
                "NW-GPS-100",
                "evt-device-1",
                DateTimeOffset.UtcNow,
                48.5,
                9.3,
                32,
                185));

        Assert.Equal(HttpStatusCode.Accepted, ingest.StatusCode);
    }

    [Fact]
    public async Task SandboxTelematicsReplayIsTenantScopedAndIdempotent()
    {
        using var adminClient = factory.CreateClient();
        var login = await adminClient.LoginAsync("admin@northwind.local", "Admin123!");
        adminClient.SetBearer(login.AccessToken);
        var connection = await adminClient.PostAsJsonAsync("/api/v1/admin/integrations/sandbox-telematics", new CreateSandboxTelematicsConnectionRequest($"Sandbox-{Guid.NewGuid():N}"));
        Assert.Equal(HttpStatusCode.Created, connection.StatusCode);
        var key = await (await adminClient.PostAsJsonAsync("/api/v1/admin/integrations/api-keys", new CreateApiClientCredentialRequest("Sandbox device", ApiClientCredentialType.Device, [IntegrationScope.DeviceTrackingWrite]))).Content.ReadFromJsonAsync<CreatedApiClientCredentialResponse>();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FleetOpsDbContext>();
        var vehicle = await db.Vehicles.SingleAsync(x => x.RegistrationNumber == "NW-100");
        using var provider = factory.CreateClient();
        provider.DefaultRequestHeaders.Add("X-Api-Key", key!.PlainTextSecret);
        var payload = new SandboxTelematicsEventRequest(SandboxTelematicsAdapter.ContractVersion, vehicle.Id, $"evt-{Guid.NewGuid():N}", "NW-GPS-100", DateTimeOffset.UtcNow, 48.5, 9.3, 30, 90, 1, 8, 1200, "position");
        var first = await provider.PostAsJsonAsync("/api/v1/integrations/device/sandbox-telematics/events", payload);
        var replay = await provider.PostAsJsonAsync("/api/v1/integrations/device/sandbox-telematics/events", payload);
        var replayBody = await replay.Content.ReadFromJsonAsync<TelemetryIngestionResponse>();
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.True(replayBody!.Duplicate);
    }

    [Fact]
    public async Task ForgedSandboxWebhookIsRejected()
    {
        using var adminClient = factory.CreateClient();
        var login = await adminClient.LoginAsync("admin@northwind.local", "Admin123!");
        adminClient.SetBearer(login.AccessToken);

        var webhook = await (await adminClient.PostAsJsonAsync(
            "/api/admin/integrations/webhooks",
            new CreateWebhookEndpointRequest(
                "Sandbox alerts",
                IntegrationEventType.AlertOpened,
                null,
                "sandbox-secret",
                true)))
            .Content.ReadFromJsonAsync<WebhookEndpointResponse>();

        using var forgedClient = factory.CreateClient();
        forgedClient.DefaultRequestHeaders.Add("X-FleetOps-Event", IntegrationEventType.AlertOpened);
        forgedClient.DefaultRequestHeaders.Add("X-FleetOps-Signature", "sha256=forged");

        var forged = await forgedClient.PostAsJsonAsync(
            $"/api/v1/integrations/sandbox/webhooks/{webhook!.Id}",
            new { alertId = Guid.NewGuid(), title = "Forged" });

        Assert.Equal(HttpStatusCode.Unauthorized, forged.StatusCode);
    }

    [Fact]
    public async Task WebhookDeliveryRetriesThenDeadLetters()
    {
        using var adminClient = factory.CreateClient();
        var login = await adminClient.LoginAsync("admin@northwind.local", "Admin123!");
        adminClient.SetBearer(login.AccessToken);

        var webhook = await (await adminClient.PostAsJsonAsync(
            "/api/admin/integrations/webhooks",
            new CreateWebhookEndpointRequest(
                "Dead letter target",
                IntegrationEventType.FleetVehicleCreated,
                "http://127.0.0.1:1/unreachable",
                "dead-letter-secret",
                false)))
            .Content.ReadFromJsonAsync<WebhookEndpointResponse>();

        var createVehicle = await adminClient.PostAsJsonAsync(
            "/api/v1/fleet/vehicles",
            new CreateVehicleRequest("NW-DL-900", "Dead letter van"));
        Assert.Equal(HttpStatusCode.Created, createVehicle.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IWebhookDispatchService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<FleetOpsDbContext>();

        await dispatcher.DispatchPendingAsync(CancellationToken.None);
        await dispatcher.DispatchPendingAsync(CancellationToken.None);
        await dispatcher.DispatchPendingAsync(CancellationToken.None);

        var outbox = await dbContext.IntegrationOutboxMessages
            .SingleAsync(x => x.WebhookEndpointId == webhook!.Id);
        var attempts = await dbContext.WebhookDeliveryAttempts
            .Where(x => x.OutboxMessageId == outbox.Id)
            .ToListAsync();

        Assert.Equal(IntegrationOutboxStatus.DeadLetter, outbox.Status);
        Assert.Equal(3, attempts.Count);
    }

    [Fact]
    public async Task FleetCsvExportsAreAvailable()
    {
        using var client = factory.CreateClient();
        var login = await client.LoginAsync("admin@northwind.local", "Admin123!");
        client.SetBearer(login.AccessToken);

        var vehicles = await client.GetStringAsync("/api/v1/fleet/vehicles/export");
        var drivers = await client.GetStringAsync("/api/v1/fleet/drivers/export");
        var devices = await client.GetStringAsync("/api/v1/fleet/devices/export");

        Assert.Contains("registrationNumber,displayName,isActive,currentOdometerKm", vehicles, StringComparison.Ordinal);
        Assert.Contains("fullName,licenseNumber,phoneNumber,isActive", drivers, StringComparison.Ordinal);
        Assert.Contains("serialNumber,displayName,isActive", devices, StringComparison.Ordinal);
    }
}
