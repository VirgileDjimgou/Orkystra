using System.Net;
using System.Net.Http.Json;
using FleetOps.Api.Auth;
using FleetOps.Api.Security;
using FleetOps.Infrastructure.Identity;
using FleetOps.UnitTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace FleetOps.UnitTests;

public sealed class ProductionSecurityTests(FleetOpsApiFactory factory) : IClassFixture<FleetOpsApiFactory>
{
    [Fact]
    public void ProductionRejectsKnownDevelopmentSecretsAndDemoSeed()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:FleetOps"] = "Server=sql;Database=FleetOps;Password=Strong-Database-Password;TrustServerCertificate=True",
                ["Jwt:SigningKey"] = "FleetOps_LocalDevelopment_ChangeThisSigningKey_123456789",
                ["ObjectStorage:MediaSigningKey"] = "FleetOps_Pilot_Signing_Key_Change_Me_123456789",
                ["Bootstrap:SeedDemoData"] = "true"
            })
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ProductionConfigurationValidator.Validate(configuration, ProductionEnvironment));

        Assert.Contains("Jwt:SigningKey", exception.Message, StringComparison.Ordinal);
        Assert.Contains("ObjectStorage:MediaSigningKey", exception.Message, StringComparison.Ordinal);
        Assert.Contains("S3-compatible private storage", exception.Message, StringComparison.Ordinal);
        Assert.Contains("SeedDemoData", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionAcceptsIndependentSecretsAndExplicitConnection()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:FleetOps"] = "Server=sql;Database=FleetOps;User Id=fleetops;Password=Independent-Database-Password;Encrypt=True",
                ["Jwt:SigningKey"] = "production-jwt-key-with-independent-random-material-2026",
                ["ObjectStorage:MediaSigningKey"] = "production-media-key-with-independent-random-material-2026",
                ["ObjectStorage:Provider"] = "S3",
                ["ObjectStorage:ServiceUrl"] = "https://objects.example.invalid",
                ["ObjectStorage:BucketName"] = "fleetops-private-media",
                ["ObjectStorage:AccessKey"] = "production-access-key",
                ["ObjectStorage:SecretKey"] = "production-secret-key",
                ["Bootstrap:SeedDemoData"] = "false"
            })
            .Build();

        ProductionConfigurationValidator.Validate(configuration, ProductionEnvironment);
    }

    [Fact]
    public async Task RepeatedInvalidPasswordsLockTheUser()
    {
        const string email = "lockout.operator@northwind.local";
        using var adminClient = factory.CreateClient();
        var adminLogin = await adminClient.LoginAsync("admin@northwind.local", "Admin123!");
        adminClient.SetBearer(adminLogin.AccessToken);
        var createResponse = await adminClient.PostAsJsonAsync(
            "/api/admin/users",
            new { email, fullName = "Lockout Operator", password = "Lockout123!", role = "Operator" });
        createResponse.EnsureSuccessStatusCode();

        using var client = factory.CreateClient();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var response = await client.PostAsJsonAsync(
                "/api/auth/login",
                new LoginRequest(email, "WrongPassword123!", null));
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        Assert.NotNull(user);
        Assert.True(await userManager.IsLockedOutAsync(user!));

        await userManager.SetLockoutEndDateAsync(user!, DateTimeOffset.UtcNow.AddMinutes(-1));
        await userManager.ResetAccessFailedCountAsync(user!);
    }

    [Fact]
    public async Task LoginEndpointRateLimitsRepeatedRequestsPerClientAddress()
    {
        using var isolatedFactory = new FleetOpsApiFactory();
        await isolatedFactory.InitializeAsync();
        using var client = isolatedFactory.CreateClient();

        for (var requestNumber = 0; requestNumber < 100; requestNumber++)
        {
            var response = await client.PostAsJsonAsync(
                "/api/auth/login",
                new LoginRequest($"unknown-{requestNumber}@example.invalid", "WrongPassword123!", null));
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        var limitedResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("unknown-limited@example.invalid", "WrongPassword123!", null));

        Assert.Equal(HttpStatusCode.TooManyRequests, limitedResponse.StatusCode);
    }

    private static IHostEnvironment ProductionEnvironment { get; } = new TestHostEnvironment();

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;

        public string ApplicationName { get; set; } = "FleetOps.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
