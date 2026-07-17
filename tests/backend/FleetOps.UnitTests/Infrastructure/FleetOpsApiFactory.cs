using FleetOps.Api;
using FleetOps.Api.Tracking;
using FleetOps.Infrastructure.Identity;
using FleetOps.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace FleetOps.UnitTests.Infrastructure;

public sealed class FleetOpsApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _databaseName = $"fleetops-tests-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration(configuration =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "FleetOps.Tests",
                ["Jwt:Audience"] = "FleetOps.Tests.Web",
                ["Jwt:SigningKey"] = "FleetOps_Tests_Signing_Key_12345678901234567890",
                ["Jwt:TokenLifetimeMinutes"] = "60",
                ["FLEETOPS_WEB_URL"] = "http://localhost:5173",
                ["Testing:UseInMemoryDatabase"] = "true",
                ["Testing:DatabaseName"] = _databaseName,
                ["Bootstrap:SeedDemoData"] = "true",
                ["Security:LoginPermitLimit"] = "100",
                ["Integrations:RetryBaseDelaySeconds"] = "0",
                ["Integrations:MaxWebhookAttempts"] = "3"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<FleetOpsDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<FleetOpsDbContext>>();

            services.AddDbContext<FleetOpsDbContext>(options => options.UseInMemoryDatabase(_databaseName));
        });
    }

    public async Task InitializeAsync()
    {
        using var _ = CreateClient();
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FleetOpsDbContext>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var metricsStore = scope.ServiceProvider.GetRequiredService<TrackingMetricsStore>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();
        metricsStore.ResetAll();
        await FleetOpsSeedData.EnsureSeededAsync(
            dbContext,
            roleManager,
            userManager,
            new BootstrapOptions { SeedDemoData = true },
            CancellationToken.None);
    }

    public new Task DisposeAsync()
    {
        return Task.CompletedTask;
    }
}
