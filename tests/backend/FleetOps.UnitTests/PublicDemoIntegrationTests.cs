using System.Net;
using System.Net.Http.Json;
using FleetOps.Api.Auth;
using FleetOps.Api.Demo;
using FleetOps.Infrastructure.Identity;
using FleetOps.Infrastructure.Persistence;
using FleetOps.UnitTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace FleetOps.UnitTests;

public sealed class PublicDemoIntegrationTests
{
    [Fact]
    public async Task DevelopmentDoesNotExposePublicLaunch()
    {
        await using var factory = new FleetOpsApiFactory();
        await factory.InitializeAsync();
        using var client = factory.CreateClient();

        var status = await client.GetFromJsonAsync<DemoStatus>("/api/v1/demo/public/status");
        var launch = await client.PostAsync("/api/v1/demo/public/launch", null);

        Assert.NotNull(status);
        Assert.False(status!.Enabled);
        Assert.Equal(HttpStatusCode.NotFound, launch.StatusCode);
    }

    [Fact]
    public async Task LaunchIssuesShortLivedHttpOnlyCsrfProtectedLeastPrivilegeSession()
    {
        await using var factory = new PublicDemoApiFactory();
        using var client = factory.CreateClient();
        var before = DateTimeOffset.UtcNow;

        var launchResponse = await client.PostAsync("/api/v1/demo/public/launch", null);
        launchResponse.EnsureSuccessStatusCode();
        var launch = await launchResponse.Content.ReadFromJsonAsync<DemoLaunchResponse>();
        var afterLaunch = DateTimeOffset.UtcNow;

        Assert.NotNull(launch);
        Assert.True(launch!.User.IsDemo);
        Assert.Equal(["Operator"], launch.User.Roles);
        Assert.Equal("FleetOps Public Demo", launch.User.OrganizationName);
        Assert.InRange(launch.ExpiresAtUtc, before.AddSeconds(1), afterLaunch.AddSeconds(31));
        Assert.Contains(
            launchResponse.Headers.GetValues("Set-Cookie"),
            value => value.Contains("fleetops-session=", StringComparison.Ordinal)
                && value.Contains("httponly", StringComparison.OrdinalIgnoreCase));

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FleetOpsDbContext>();
            var users = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>();
            Assert.Single(await db.Organizations.ToListAsync());
            var demoUser = await users.FindByEmailAsync(PublicDemoOptions.OperatorEmail);
            Assert.NotNull(demoUser);
            Assert.False(await users.HasPasswordAsync(demoUser!));
        }

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(
            "/api/v1/demo/session/control",
            new DemoControlRequest("START", "LATE_DELIVERY"))).StatusCode);

        client.DefaultRequestHeaders.Add("X-CSRF-Token", launch.CsrfToken);
        var control = await client.PostAsJsonAsync(
            "/api/v1/demo/session/control",
            new DemoControlRequest("START", "LATE_DELIVERY"));
        control.EnsureSuccessStatusCode();
        var state = await control.Content.ReadFromJsonAsync<DemoSessionState>();
        Assert.Equal("RUNNING", state!.Status);
        Assert.Equal("LATE_DELIVERY", state.Scenario);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/auth/sessions")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(
            "/api/v1/fleet/vehicles",
            new { registrationNumber = "FORBIDDEN", displayName = "Forbidden" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/users")).StatusCode);
    }

    [Fact]
    public async Task ConcurrentVisitorControlsRemainSessionIsolated()
    {
        await using var factory = new PublicDemoApiFactory();
        using var first = factory.CreateClient();
        using var second = factory.CreateClient();
        var firstLaunch = await LaunchAsync(first);
        var secondLaunch = await LaunchAsync(second);
        first.DefaultRequestHeaders.Add("X-CSRF-Token", firstLaunch.CsrfToken);
        second.DefaultRequestHeaders.Add("X-CSRF-Token", secondLaunch.CsrfToken);

        (await first.PostAsJsonAsync(
            "/api/v1/demo/session/control",
            new DemoControlRequest("START", "VEHICLE_ISSUE"))).EnsureSuccessStatusCode();

        var firstState = await first.GetFromJsonAsync<DemoSessionState>("/api/v1/demo/session/control");
        var secondState = await second.GetFromJsonAsync<DemoSessionState>("/api/v1/demo/session/control");
        Assert.Equal("VEHICLE_ISSUE", firstState!.Scenario);
        Assert.Equal("RUNNING", firstState.Status);
        Assert.Equal("NORMAL_SHIFT", secondState!.Scenario);
        Assert.Equal("READY", secondState.Status);
    }

    [Fact]
    public async Task LaunchIsRateLimitedAndExpiredSessionIsRejected()
    {
        await using var limitedFactory = new PublicDemoApiFactory(sessionLifetimeSeconds: 1, launchPermitLimit: 1);
        using var client = limitedFactory.CreateClient();
        var launch = await LaunchAsync(client);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsync("/api/v1/demo/public/launch", null)).StatusCode);

        await Task.Delay(TimeSpan.FromMilliseconds(1200));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/demo/session/control")).StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(launch.CsrfToken));
    }

    private static async Task<DemoLaunchResponse> LaunchAsync(HttpClient client)
    {
        var response = await client.PostAsync("/api/v1/demo/public/launch", null);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<DemoLaunchResponse>())!;
    }

    private sealed record DemoStatus(bool Enabled);

    private sealed class PublicDemoApiFactory(int sessionLifetimeSeconds = 30, int launchPermitLimit = 100)
        : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"fleetops-public-demo-{Guid.NewGuid():N}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Demo");
            builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Jwt:Issuer"] = "FleetOps.Tests",
                    ["Jwt:Audience"] = "FleetOps.Tests.Web",
                    ["Jwt:SigningKey"] = "FleetOps_Tests_Signing_Key_12345678901234567890",
                    ["Testing:UseInMemoryDatabase"] = "true",
                    ["Testing:DatabaseName"] = _databaseName,
                    ["Bootstrap:SeedDemoData"] = "true",
                    ["Bootstrap:PublicDemoOnly"] = "true",
                    ["PublicDemo:Enabled"] = "true",
                    ["PublicDemo:SideEffectsSandboxed"] = "true",
                    ["PublicDemo:SessionLifetimeSeconds"] = sessionLifetimeSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["PublicDemo:LaunchPermitLimit"] = launchPermitLimit.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["PublicDemo:MaxConcurrentSessions"] = "20",
                    ["ObjectStorage:Provider"] = "FileSystem",
                }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<FleetOpsDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<FleetOpsDbContext>>();
                services.AddDbContext<FleetOpsDbContext>(options => options.UseInMemoryDatabase(_databaseName));
            });
        }
    }
}
