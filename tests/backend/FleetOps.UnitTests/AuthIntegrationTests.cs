using System.Net;
using System.Net.Http.Json;
using FleetOps.Api;
using FleetOps.Api.Admin;
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
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FleetOpsDbContext>();
        var northwind = await dbContext.Organizations.SingleAsync(x => x.Slug == "northwind");
        var southridge = await dbContext.Organizations.SingleAsync(x => x.Slug == "southridge");

        using var simulationClient = factory.CreateClient();
        await simulationClient.PostAsJsonAsync("/api/simulation/telemetry", new TelemetryContract(
            northwind.Id,
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "SIM-1",
            DateTimeOffset.UtcNow,
            48.4,
            9.2,
            30,
            180));
        await simulationClient.PostAsJsonAsync("/api/simulation/telemetry", new TelemetryContract(
            southridge.Id,
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            "SIM-2",
            DateTimeOffset.UtcNow,
            48.5,
            9.3,
            35,
            200));

        using var client = factory.CreateClient();
        var login = await client.LoginAsync("admin@northwind.local", "Admin123!");
        client.SetBearer(login.AccessToken);

        var response = await client.GetFromJsonAsync<List<TelemetryContract>>("/api/tracking/latest");

        Assert.NotNull(response);
        Assert.Single(response!);
        Assert.All(response!, point => Assert.Equal(northwind.Id, point.OrganizationId));
    }
}
