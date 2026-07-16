using System.Net.Http.Json;
using System.Text.Json;
using FleetOps.Api.Admin;
using FleetOps.Core.Modules.Integrations;
using FleetOps.Core.Modules.Operations;
using FleetOps.Core.Modules.Tracking;
using FleetOps.Infrastructure.Persistence;
using FleetOps.UnitTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FleetOps.UnitTests;

public sealed class ProductionReadinessIntegrationTests(FleetOpsApiFactory factory) : IClassFixture<FleetOpsApiFactory>
{
    [Fact]
    public async Task DataLifecycleSummaryAndExportRemainTenantScoped()
    {
        using var client = factory.CreateClient();
        var login = await client.LoginAsync("admin@northwind.local", "Admin123!");
        client.SetBearer(login.AccessToken!);

        var summary = await client.GetFromJsonAsync<DataLifecycleSummaryResponse>("/api/admin/data-lifecycle/summary");
        Assert.NotNull(summary);
        Assert.Equal("northwind", summary!.OrganizationSlug);
        Assert.Contains(summary.Counts, count => count.Key == "users" && count.Count >= 3);
        Assert.Contains(summary.Categories, category => category.Key == "tracking-history");

        var exportResponse = await client.GetAsync("/api/admin/data-lifecycle/export");
        exportResponse.EnsureSuccessStatusCode();
        using var exportJson = JsonDocument.Parse(await exportResponse.Content.ReadAsStringAsync());
        var root = exportJson.RootElement;
        Assert.Equal("northwind", root.GetProperty("organization").GetProperty("slug").GetString());

        var users = root.GetProperty("users").EnumerateArray().Select(x => x.GetProperty("email").GetString()).ToArray();
        Assert.Contains("admin@northwind.local", users);
        Assert.DoesNotContain("admin@southridge.local", users);
    }

    [Fact]
    public async Task DataLifecyclePurgeDeletesSelectedHistoricalArtifacts()
    {
        Guid webhookId;
        Guid outboxId;
        Guid uploadSessionId;

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FleetOpsDbContext>();
            var organization = await dbContext.Organizations.SingleAsync(x => x.Slug == "northwind");
            var vehicle = await dbContext.Vehicles.SingleAsync(x => x.OrganizationId == organization.Id && x.RegistrationNumber == "NW-100");
            var device = await dbContext.GpsDevices.SingleAsync(x => x.OrganizationId == organization.Id && x.SerialNumber == "NW-GPS-100");
            var driver = await dbContext.Drivers.SingleAsync(x => x.OrganizationId == organization.Id && x.LicenseNumber == "NW-DL-001");
            var oldUtc = new DateTimeOffset(2026, 5, 1, 8, 0, 0, TimeSpan.Zero);

            dbContext.TelemetryPoints.Add(new TelemetryPoint(
                organization.Id,
                vehicle.Id,
                device.SerialNumber,
                "purge-history-1",
                oldUtc,
                48.1,
                9.2,
                0,
                120,
                oldUtc));
            dbContext.CurrentVehiclePositions.Add(new CurrentVehiclePosition(
                organization.Id,
                vehicle.Id,
                device.SerialNumber,
                "purge-position-1",
                oldUtc,
                48.1,
                9.2,
                0,
                120));

            var webhook = new WebhookEndpoint(
                organization.Id,
                "Historical delivery",
                "fleet.vehicle.created",
                "https://partner.example/webhooks/fleetops",
                "secret",
                false);
            dbContext.WebhookEndpoints.Add(webhook);
            webhookId = webhook.Id;

            var outbox = new IntegrationOutboxMessage(
                organization.Id,
                webhook.Id,
                "fleet.vehicle.created",
                "vehicle",
                vehicle.Id.ToString(),
                "{\"vehicleId\":\"veh-1\"}",
                oldUtc);
            outbox.MarkDelivered(oldUtc.AddMinutes(5));
            dbContext.IntegrationOutboxMessages.Add(outbox);
            outboxId = outbox.Id;

            dbContext.WebhookDeliveryAttempts.Add(new WebhookDeliveryAttempt(
                organization.Id,
                outbox.Id,
                webhook.Id,
                1,
                200,
                "{\"ok\":true}",
                true,
                oldUtc.AddMinutes(1)));
            dbContext.SandboxWebhookReceipts.Add(new SandboxWebhookReceipt(
                organization.Id,
                webhook.Id,
                "fleet.vehicle.created",
                "sha256=test",
                "{\"vehicleId\":\"veh-1\"}",
                oldUtc.AddMinutes(2)));

            dbContext.MediaUploadSessions.Add(new MediaUploadSession(
                organization.Id,
                driver.Id,
                MediaUploadPurpose.InspectionPhoto,
                "old-proof.jpg",
                "image/jpeg",
                1024,
                oldUtc,
                "temp/old-proof.jpg"));
            uploadSessionId = dbContext.ChangeTracker.Entries<MediaUploadSession>()
                .Single(x => x.Entity.FileName == "old-proof.jpg")
                .Entity.Id;

            await dbContext.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        var login = await client.LoginAsync("admin@northwind.local", "Admin123!");
        client.SetBearer(login.AccessToken!);

        var purgeResponse = await client.PostAsJsonAsync(
            "/api/admin/data-lifecycle/purge",
            new PurgeLifecycleDataRequest(
                "northwind",
                new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
                ["tracking-history", "integration-history", "upload-sessions"]));
        purgeResponse.EnsureSuccessStatusCode();

        var result = await purgeResponse.Content.ReadFromJsonAsync<PurgeLifecycleDataResponse>();
        Assert.NotNull(result);
        Assert.True(result!.TotalDeleted >= 5);
        Assert.Contains(result.Results, x => x.Key == "tracking-history" && x.DeletedCount == 2);
        Assert.Contains(result.Results, x => x.Key == "integration-history" && x.DeletedCount == 3);
        Assert.Contains(result.Results, x => x.Key == "upload-sessions" && x.DeletedCount == 1);

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<FleetOpsDbContext>();
        Assert.False(await verificationDb.TelemetryPoints.AnyAsync(x => x.EventId == "purge-history-1"));
        Assert.False(await verificationDb.CurrentVehiclePositions.AnyAsync(x => x.EventId == "purge-position-1"));
        Assert.False(await verificationDb.WebhookDeliveryAttempts.AnyAsync(x => x.OutboxMessageId == outboxId && x.WebhookEndpointId == webhookId));
        Assert.False(await verificationDb.SandboxWebhookReceipts.AnyAsync(x => x.WebhookEndpointId == webhookId && x.EventType == "fleet.vehicle.created"));
        Assert.False(await verificationDb.IntegrationOutboxMessages.AnyAsync(x => x.Id == outboxId));
        Assert.False(await verificationDb.MediaUploadSessions.AnyAsync(x => x.Id == uploadSessionId));
        Assert.Contains(
            verificationDb.AuditLogs,
            x => x.ActionType == "admin.data_lifecycle.purged");
    }
}
