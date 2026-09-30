using System.Net;
using System.Net.Http.Json;
using FleetOps.Api.Auth;
using FleetOps.Api.Demo;
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

[Trait("Category", "Reliability")]
public sealed class PublicDemoSecurityLoadTests
{
    [Fact]
    public async Task ConcurrentLaunchBurstRespectsPermitLimitWithoutServerErrors()
    {
        await using var factory = new SecurityLoadDemoFactory(launchPermitLimit: 3);
        using var client = factory.CreateClient();

        var responses = await Task.WhenAll(Enumerable.Range(0, 10)
            .Select(_ => client.PostAsync("/api/v1/demo/public/launch", null)));

        Assert.Equal(3, responses.Count(response => response.StatusCode == HttpStatusCode.OK));
        Assert.All(responses, response => Assert.True(
            response.StatusCode is HttpStatusCode.OK or HttpStatusCode.TooManyRequests,
            $"Unexpected launch status {(int)response.StatusCode}."));
    }

    [Fact]
    public async Task ExpiredSessionIsRejectedUnderConcurrentReads()
    {
        await using var factory = new SecurityLoadDemoFactory(sessionLifetimeSeconds: 1);
        using var client = factory.CreateClient();
        var launchResponse = await client.PostAsync("/api/v1/demo/public/launch", null);
        launchResponse.EnsureSuccessStatusCode();

        await Task.Delay(TimeSpan.FromMilliseconds(1200));

        var reads = await Task.WhenAll(Enumerable.Range(0, 10)
            .Select(_ => client.GetAsync("/api/v1/tracking/positions")));
        Assert.All(reads, response => Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode));

        var me = await client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    [Fact]
    public async Task DemoSandboxDeniesMutationsWhileReadsRemainAvailable()
    {
        await using var factory = new SecurityLoadDemoFactory();
        using var client = factory.CreateClient();
        var launchResponse = await client.PostAsync("/api/v1/demo/public/launch", null);
        launchResponse.EnsureSuccessStatusCode();
        var launch = (await launchResponse.Content.ReadFromJsonAsync<DemoLaunchResponse>())!;
        client.DefaultRequestHeaders.Add("X-CSRF-Token", launch.CsrfToken);

        var writes = await Task.WhenAll(Enumerable.Range(0, 10).Select(index =>
            client.PostAsJsonAsync("/api/v1/fleet/vehicles", new { registrationNumber = $"DEMO-LOAD-{index}", displayName = "Forbidden" })));
        Assert.All(writes, response => Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode));

        var reads = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ =>
            client.GetAsync("/api/v1/tracking/positions")));
        Assert.All(reads, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
    }

    private sealed class SecurityLoadDemoFactory(
        int sessionLifetimeSeconds = 30,
        int launchPermitLimit = 100,
        int maxConcurrentSessions = 20) : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"fleetops-security-load-{Guid.NewGuid():N}";

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
                    ["PublicDemo:MaxConcurrentSessions"] = maxConcurrentSessions.ToString(System.Globalization.CultureInfo.InvariantCulture),
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
