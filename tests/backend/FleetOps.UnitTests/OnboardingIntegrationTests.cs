using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using FleetOps.Api.Onboarding;
using FleetOps.Core.Modules.Onboarding;
using FleetOps.Infrastructure.Persistence;
using FleetOps.UnitTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FleetOps.UnitTests;

public sealed class OnboardingIntegrationTests(FleetOpsApiFactory factory) : IClassFixture<FleetOpsApiFactory>
{
    [Fact]
    public async Task InvitationIsTenantScopedAndCanOnlyBeAcceptedOnce()
    {
        using var admin = factory.CreateClient();
        admin.SetBearer((await admin.LoginAsync("admin@northwind.local", "Admin123!")).AccessToken);
        var created = await admin.PostAsJsonAsync("/api/v1/onboarding/invitations", new CreateInvitationRequest("new.operator@northwind.local", "New Operator", "Operator"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var invitation = await created.Content.ReadFromJsonAsync<InvitationResponse>();
        Assert.NotNull(invitation);

        using var publicClient = factory.CreateClient();
        var accepted = await publicClient.PostAsJsonAsync("/api/v1/onboarding/invitations/accept", new AcceptInvitationRequest(invitation!.Token, "Operator123!"));
        Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);
        var replay = await publicClient.PostAsJsonAsync("/api/v1/onboarding/invitations/accept", new AcceptInvitationRequest(invitation.Token, "Operator123!"));
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
    }

    [Fact]
    public async Task PairingCodeIsShortOneUseAndCannotCrossTenantBoundary()
    {
        using var northwindAdmin = factory.CreateClient();
        northwindAdmin.SetBearer((await northwindAdmin.LoginAsync("admin@northwind.local", "Admin123!")).AccessToken);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FleetOpsDbContext>();
        var driverId = await db.Users.Where(x => x.Email == "driver@northwind.local").Select(x => x.Id).SingleAsync();
        var created = await northwindAdmin.PostAsJsonAsync("/api/v1/onboarding/pairing-codes", new CreatePairingCodeRequest(driverId));
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var code = await created.Content.ReadFromJsonAsync<PairingCodeResponse>();

        using var anonymous = factory.CreateClient();
        var paired = await anonymous.PostAsJsonAsync("/api/v1/onboarding/driver-pairing/consume", new ConsumePairingCodeRequest(code!.Code));
        Assert.Equal(HttpStatusCode.OK, paired.StatusCode);
        var replay = await anonymous.PostAsJsonAsync("/api/v1/onboarding/driver-pairing/consume", new ConsumePairingCodeRequest(code.Code));
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);

        using var southridgeAdmin = factory.CreateClient();
        southridgeAdmin.SetBearer((await southridgeAdmin.LoginAsync("admin@southridge.local", "Admin123!")).AccessToken);
        var crossTenant = await southridgeAdmin.PostAsJsonAsync("/api/v1/onboarding/pairing-codes", new CreatePairingCodeRequest(driverId));
        Assert.Equal(HttpStatusCode.NotFound, crossTenant.StatusCode);
    }

    [Fact]
    public async Task ExpiredInvitationCannotBeAccepted()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FleetOpsDbContext>();
        var organizationId = await db.Organizations.Where(x => x.Slug == "northwind").Select(x => x.Id).SingleAsync();
        var invitation = new TenantInvitation(
            organizationId,
            "expired@northwind.local",
            "Expired User",
            "Operator",
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("expired-token"))),
            DateTimeOffset.UtcNow.AddMinutes(-1));
        db.Add(invitation);
        await db.SaveChangesAsync();
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/onboarding/invitations/accept", new AcceptInvitationRequest("expired-token", "Operator123!"));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task InvalidPreviewReportsEachLineAndConfirmationWritesNoFleetData()
    {
        using var admin = factory.CreateClient();
        admin.SetBearer((await admin.LoginAsync("admin@northwind.local", "Admin123!")).AccessToken);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FleetOpsDbContext>();
        var before = await db.Vehicles.CountAsync(x => x.RegistrationNumber.StartsWith("ONB-INVALID-"));

        var previewResponse = await admin.PostAsJsonAsync(
            "/api/v1/onboarding/imports/preview",
            new ImportPreviewRequest(
                "vehicles",
                "registrationNumber,displayName\nONB-INVALID-1,Valid row\n,Missing key\nONB-INVALID-3"));
        previewResponse.EnsureSuccessStatusCode();
        var preview = await previewResponse.Content.ReadFromJsonAsync<ImportPreviewResponse>();

        Assert.NotNull(preview);
        Assert.False(preview!.CanConfirm);
        Assert.Contains(preview.Errors, x => x.Line == 3 && x.Field == "registrationNumber");
        Assert.Contains(preview.Errors, x => x.Line == 4 && x.Field == "row");
        var confirmation = await admin.PostAsJsonAsync(
            $"/api/v1/onboarding/imports/{preview.PreviewId}/confirm",
            new ConfirmImportRequest());
        Assert.Equal(HttpStatusCode.Conflict, confirmation.StatusCode);
        Assert.Equal(before, await db.Vehicles.CountAsync(x => x.RegistrationNumber.StartsWith("ONB-INVALID-")));
    }

    [Fact]
    public async Task ConfirmedBulkImportIsIdempotentAndTenantScoped()
    {
        using var admin = factory.CreateClient();
        admin.SetBearer((await admin.LoginAsync("admin@northwind.local", "Admin123!")).AccessToken);
        var csv = new StringBuilder("registrationNumber,displayName\n");
        for (var index = 1; index <= 100; index++)
        {
            csv.Append(CultureInfo.InvariantCulture, $"ONB-BULK-{index:D3},Onboarding vehicle {index}\n");
        }

        var previewResponse = await admin.PostAsJsonAsync(
            "/api/v1/onboarding/imports/preview",
            new ImportPreviewRequest("vehicles", csv.ToString()));
        var preview = await previewResponse.Content.ReadFromJsonAsync<ImportPreviewResponse>();
        Assert.True(preview!.CanConfirm);
        Assert.Equal(100, preview.RowCount);

        using var southridgeAdmin = factory.CreateClient();
        southridgeAdmin.SetBearer((await southridgeAdmin.LoginAsync("admin@southridge.local", "Admin123!")).AccessToken);
        var crossTenant = await southridgeAdmin.PostAsJsonAsync(
            $"/api/v1/onboarding/imports/{preview.PreviewId}/confirm",
            new ConfirmImportRequest());
        Assert.Equal(HttpStatusCode.NotFound, crossTenant.StatusCode);

        var first = await admin.PostAsJsonAsync(
            $"/api/v1/onboarding/imports/{preview.PreviewId}/confirm",
            new ConfirmImportRequest());
        first.EnsureSuccessStatusCode();
        var firstSummary = await first.Content.ReadFromJsonAsync<ConfirmImportResponse>();
        Assert.Equal(100, firstSummary!.Created);
        Assert.False(firstSummary.WasAlreadyConfirmed);

        var replay = await admin.PostAsJsonAsync(
            $"/api/v1/onboarding/imports/{preview.PreviewId}/confirm",
            new ConfirmImportRequest());
        replay.EnsureSuccessStatusCode();
        var replaySummary = await replay.Content.ReadFromJsonAsync<ConfirmImportResponse>();
        Assert.True(replaySummary!.WasAlreadyConfirmed);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FleetOpsDbContext>();
        Assert.Equal(100, await db.Vehicles.CountAsync(x => x.RegistrationNumber.StartsWith("ONB-BULK-")));
    }

    [Fact]
    public async Task SampleDataIsIsolatedAndExplicitlyRemovable()
    {
        using var admin = factory.CreateClient();
        admin.SetBearer((await admin.LoginAsync("admin@southridge.local", "Admin123!")).AccessToken);
        await using var beforeScope = factory.Services.CreateAsyncScope();
        var beforeDb = beforeScope.ServiceProvider.GetRequiredService<FleetOpsDbContext>();
        var organizationId = await beforeDb.Organizations.Where(x => x.Slug == "southridge").Select(x => x.Id).SingleAsync();
        var beforeVehicles = await beforeDb.Vehicles.CountAsync(x => x.OrganizationId == organizationId);

        var create = await admin.PostAsync("/api/v1/onboarding/sample-data", null);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var sample = await create.Content.ReadFromJsonAsync<SampleDataResponse>();
        Assert.NotNull(sample);

        var remove = await admin.DeleteAsync("/api/v1/onboarding/sample-data");
        Assert.Equal(HttpStatusCode.NoContent, remove.StatusCode);
        await using var afterScope = factory.Services.CreateAsyncScope();
        var afterDb = afterScope.ServiceProvider.GetRequiredService<FleetOpsDbContext>();
        Assert.Equal(beforeVehicles, await afterDb.Vehicles.CountAsync(x => x.OrganizationId == organizationId));
        Assert.False(await afterDb.OnboardingSampleDataSets.AnyAsync(x => x.OrganizationId == organizationId));
    }

    [Fact]
    public async Task DiagnosticsContainCountsButNoPersonalData()
    {
        using var admin = factory.CreateClient();
        admin.SetBearer((await admin.LoginAsync("admin@northwind.local", "Admin123!")).AccessToken);
        var response = await admin.GetAsync("/api/v1/onboarding/diagnostics");
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("organizationId", content, StringComparison.Ordinal);
        Assert.DoesNotContain("admin@northwind.local", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fullName", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DriverInvitationLinksImportedProfileAndEnablesPairing()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        using var admin = factory.CreateClient();
        admin.SetBearer((await admin.LoginAsync("admin@northwind.local", "Admin123!")).AccessToken);
        var previewResponse = await admin.PostAsJsonAsync(
            "/api/v1/onboarding/imports/preview",
            new ImportPreviewRequest(
                "drivers",
                $"fullName,licenseNumber,phoneNumber\nOnboarding Driver,ONB-LIC-{suffix},"));
        var preview = await previewResponse.Content.ReadFromJsonAsync<ImportPreviewResponse>();
        var confirm = await admin.PostAsJsonAsync(
            $"/api/v1/onboarding/imports/{preview!.PreviewId}/confirm",
            new ConfirmImportRequest(preview.RowVersion));
        confirm.EnsureSuccessStatusCode();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FleetOpsDbContext>();
        var driverId = await db.Drivers
            .Where(x => x.LicenseNumber == $"ONB-LIC-{suffix}")
            .Select(x => x.Id)
            .SingleAsync();
        var email = $"driver.{suffix}@northwind.local";
        var invitationResponse = await admin.PostAsJsonAsync(
            "/api/v1/onboarding/invitations",
            new CreateInvitationRequest(email, "Onboarding Driver", "Driver", driverId));
        invitationResponse.EnsureSuccessStatusCode();
        var invitation = await invitationResponse.Content.ReadFromJsonAsync<InvitationResponse>();
        using var anonymous = factory.CreateClient();
        var accepted = await anonymous.PostAsJsonAsync(
            "/api/v1/onboarding/invitations/accept",
            new AcceptInvitationRequest(invitation!.Token, "Driver123!"));
        Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);

        var userId = await db.Users.Where(x => x.Email == email).Select(x => x.Id).SingleAsync();
        var pairing = await admin.PostAsJsonAsync(
            "/api/v1/onboarding/pairing-codes",
            new CreatePairingCodeRequest(userId));
        Assert.Equal(HttpStatusCode.OK, pairing.StatusCode);
    }
}
