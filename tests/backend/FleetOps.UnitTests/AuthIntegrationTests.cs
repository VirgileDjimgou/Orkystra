using System.Net;
using System.Net.Http.Json;
using FleetOps.Api;
using FleetOps.Api.Admin;
using FleetOps.Api.Tracking;
using FleetOps.Core.Modules.Identity;
using FleetOps.Infrastructure.Persistence;
using FleetOps.UnitTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FleetOps.UnitTests;

public sealed class AuthIntegrationTests(FleetOpsApiFactory factory) : IClassFixture<FleetOpsApiFactory>
{
    [Fact]
    public async Task AdminUserListIsRestrictedToCurrentOrganization()
    {
        using var client = factory.CreateClient();
        var login = await client.LoginAsync("admin@northwind.local", "Admin123!");
        client.SetBearer(login.AccessToken);

        var response = await client.GetFromJsonAsync<List<UserSummaryResponse>>("/api/admin/users");

        Assert.NotNull(response);
        Assert.NotEmpty(response);
        Assert.All(response!, user => Assert.EndsWith("@northwind.local", user.Email, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(response!, user => user.Email.Equals("admin@southridge.local", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task OperatorCannotAccessUserAdministration()
    {
        using var client = factory.CreateClient();
        var login = await client.LoginAsync("operator@northwind.local", "Operator123!");
        client.SetBearer(login.AccessToken);

        var response = await client.GetAsync("/api/admin/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task InvalidAndExpiredTokensAreRejected()
    {
        using var invalidClient = factory.CreateClient();
        invalidClient.SetBearer("not-a-token");
        var invalidResponse = await invalidClient.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, invalidResponse.StatusCode);

        using var expiredClient = factory.CreateClient();
        var expiredToken = AuthTestExtensions.CreateExpiredToken(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "expired@test.local",
            SystemRoles.Admin);
        expiredClient.SetBearer(expiredToken);

        var expiredResponse = await expiredClient.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, expiredResponse.StatusCode);
    }

    [Fact]
    public async Task LoginAndAdministrativeActionsAreAudited()
    {
        using var client = factory.CreateClient();
        var login = await client.LoginAsync("admin@northwind.local", "Admin123!");
        client.SetBearer(login.AccessToken);

        var createResponse = await client.PostAsJsonAsync("/api/admin/users", new CreateUserRequest(
            "dispatcher@northwind.local",
            "Northwind Dispatcher",
            "Dispatcher123!",
            SystemRoles.Operator));
        createResponse.EnsureSuccessStatusCode();

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FleetOpsDbContext>();
        var auditEntries = dbContext.AuditLogs.OrderBy(x => x.OccurredAtUtc).ToList();

        Assert.Contains(auditEntries, x => x.ActionType == "auth.login");
        Assert.Contains(auditEntries, x => x.ActionType == "admin.user_created");
    }

    [Fact]
    public async Task TrackingEndpointFiltersTelemetryByTenant()
    {
        using var simulationClient = factory.CreateClient();
        await simulationClient.PostAsync("/api/internal/v1/tracking/scenarios/northwind/reset", null);
        var northwind = await simulationClient.GetFromJsonAsync<TrackingScenarioResponse>("/api/internal/v1/tracking/scenarios/northwind");
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FleetOpsDbContext>();
        var southridge = await dbContext.Organizations.SingleAsync(x => x.Slug == "southridge");
        var southVehicle = await dbContext.Vehicles
            .SingleAsync(x => x.OrganizationId == southridge.Id && x.RegistrationNumber == "SR-200");
        var southDevice = await dbContext.GpsDevices
            .SingleAsync(x => x.OrganizationId == southridge.Id && x.SerialNumber == "SR-GPS-200");
        var now = DateTimeOffset.UtcNow;

        await simulationClient.PostAsJsonAsync("/api/internal/v1/tracking/events", new IngestTelemetryRequest(
            northwind!.OrganizationId,
            northwind.Vehicles[0].VehicleId,
            northwind.Vehicles[0].DeviceId,
            "north-auth-1",
            now,
            48.4,
            9.2,
            30,
            180));
        await simulationClient.PostAsJsonAsync("/api/internal/v1/tracking/events", new IngestTelemetryRequest(
            southridge.Id,
            southVehicle.Id,
            southDevice.SerialNumber,
            "south-auth-1",
            now,
            48.5,
            9.3,
            35,
            200));

        using var client = factory.CreateClient();
        var login = await client.LoginAsync("admin@northwind.local", "Admin123!");
        client.SetBearer(login.AccessToken);

        var response = await client.GetFromJsonAsync<List<TrackingPositionResponse>>("/api/v1/tracking/positions");

        Assert.NotNull(response);
        Assert.Single(response!);
        Assert.All(response!, point => Assert.StartsWith("NW-", point.RegistrationNumber, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AdminCanEnableMfaAndThenMustProvideAuthenticatorCodeAtLogin()
    {
        using var adminClient = factory.CreateClient();
        var login = await adminClient.LoginAsync("admin@northwind.local", "Admin123!");
        adminClient.SetBearer(login.AccessToken!);

        var setupResponse = await adminClient.PostAsync("/api/admin/security/mfa/setup", null);
        setupResponse.EnsureSuccessStatusCode();
        var setup = await setupResponse.Content.ReadFromJsonAsync<MfaSetupResponse>();

        Assert.NotNull(setup);
        Assert.False(setup!.IsEnabled);
        Assert.False(string.IsNullOrWhiteSpace(setup.ManualEntryKey));
        Assert.StartsWith("otpauth://totp/", setup.AuthenticatorUri, StringComparison.Ordinal);

        var verificationCode = AuthTestExtensions.CreateAuthenticatorCode(setup.ManualEntryKey);
        var verifyResponse = await adminClient.PostAsJsonAsync(
            "/api/admin/security/mfa/verify",
            new VerifyMfaRequest(verificationCode));
        verifyResponse.EnsureSuccessStatusCode();

        var verified = await verifyResponse.Content.ReadFromJsonAsync<VerifyMfaResponse>();
        Assert.NotNull(verified);
        Assert.True(verified!.IsEnabled);
        Assert.Equal(8, verified.RecoveryCodes.Length);

        using var challengedClient = factory.CreateClient();
        var challengedLogin = await challengedClient.RequestLoginAsync("admin@northwind.local", "Admin123!");
        Assert.True(challengedLogin.RequiresTwoFactor);
        Assert.Equal("authenticator", challengedLogin.TwoFactorProvider);
        Assert.Contains("authenticator", challengedLogin.ChallengeMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(string.Empty, challengedLogin.AccessToken);

        var completedLogin = await challengedClient.LoginAsync(
            "admin@northwind.local",
            "Admin123!",
            AuthTestExtensions.CreateAuthenticatorCode(setup.ManualEntryKey));
        Assert.False(completedLogin.RequiresTwoFactor);
        Assert.NotNull(completedLogin.AccessToken);
        Assert.True(completedLogin.User!.TwoFactorEnabled);

        var disableResponse = await adminClient.PostAsJsonAsync(
            "/api/admin/security/mfa/disable",
            new DisableMfaRequest(AuthTestExtensions.CreateAuthenticatorCode(setup.ManualEntryKey)));
        disableResponse.EnsureSuccessStatusCode();
        var disabled = await disableResponse.Content.ReadFromJsonAsync<MfaStatusResponse>();

        Assert.NotNull(disabled);
        Assert.False(disabled!.IsEnabled);
        Assert.False(disabled.HasSharedKey);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FleetOpsDbContext>();
        var auditEntries = dbContext.AuditLogs.OrderBy(x => x.OccurredAtUtc).ToList();
        Assert.Contains(auditEntries, x => x.ActionType == "admin.security.mfa_setup_generated");
        Assert.Contains(auditEntries, x => x.ActionType == "admin.security.mfa_enabled");
        Assert.Contains(auditEntries, x => x.ActionType == "admin.security.mfa_disabled");
    }
}
