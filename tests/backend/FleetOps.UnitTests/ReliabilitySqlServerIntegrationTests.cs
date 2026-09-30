using System.Net;
using System.Net.Http.Json;
using FleetOps.Api.Tracking;
using FleetOps.Infrastructure.Persistence;
using FleetOps.UnitTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace FleetOps.UnitTests;

[Trait("Category", "SqlServer")]
public sealed class ReliabilitySqlServerIntegrationTests(FleetOpsSqlServerApiFactory factory)
    : IClassFixture<FleetOpsSqlServerApiFactory>
{
    [RequiresDockerFact]
    public async Task ApiRestartRestoresCurrentPositionsAndHistory()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        var scenario = await LoadScenarioAsync(client, "northwind", 20);
        var startUtc = DateTimeOffset.UtcNow.AddMinutes(-5);
        for (var tick = 1; tick <= 10; tick++)
        {
            foreach (var (vehicle, index) in scenario.Vehicles.Select((vehicle, index) => (vehicle, index)))
            {
                var response = await PostTelemetryAsync(client, scenario, vehicle, $"restart-{index:D2}-{tick:D4}", startUtc.AddSeconds(tick * 5), tick);
                Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
            }
        }

        var login = await client.LoginAsync("operator@northwind.local", "Operator123!");
        client.SetBearer(login.AccessToken);
        var before = await client.GetFromJsonAsync<List<TrackingPositionResponse>>("/api/v1/tracking/positions");
        Assert.Equal(20, before!.Count);

        await using (var restarted = new RestartedApiFactory(factory.ConnectionString))
        {
            using var restartedClient = restarted.CreateClient();
            var restartedLogin = await restartedClient.LoginAsync("operator@northwind.local", "Operator123!");
            restartedClient.SetBearer(restartedLogin.AccessToken);

            var after = await restartedClient.GetFromJsonAsync<List<TrackingPositionResponse>>("/api/v1/tracking/positions");
            Assert.Equal(
                before.OrderBy(x => x.VehicleId).Select(x => (x.VehicleId, x.SequenceNumber, x.RecordedAtUtc)),
                after!.OrderBy(x => x.VehicleId).Select(x => (x.VehicleId, x.SequenceNumber, x.RecordedAtUtc)));

            var history = await restartedClient.GetFromJsonAsync<TrackingHistoryPageResponse>(
                $"/api/v1/tracking/history?vehicleId={scenario.Vehicles[0].VehicleId}&page=1&pageSize=5");
            Assert.Equal(10, history!.TotalCount);
        }
    }

    [RequiresDockerFact]
    public async Task ConcurrentScenarioResetsRemainPredictableAndBounded()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        var scenario = await LoadScenarioAsync(client, "northwind", 20);
        var startUtc = DateTimeOffset.UtcNow.AddMinutes(-5);
        for (var tick = 1; tick <= 5; tick++)
        {
            foreach (var vehicle in scenario.Vehicles)
            {
                var response = await PostTelemetryAsync(client, scenario, vehicle, $"reset-race-{vehicle.RegistrationNumber}-{tick}", startUtc.AddSeconds(tick * 5), tick);
                Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
            }
        }

        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ =>
            client.PostAsync("/api/internal/v1/tracking/scenarios/northwind/reset", null)));
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        var bodies = await Task.WhenAll(responses.Select(response => response.Content.ReadFromJsonAsync<TrackingScenarioResetResponse>()));
        Assert.Equal(100, bodies.Sum(body => body!.DeletedHistoryPoints));
        Assert.Equal(20, bodies.Sum(body => body!.DeletedCurrentPositions));

        var login = await client.LoginAsync("operator@northwind.local", "Operator123!");
        client.SetBearer(login.AccessToken);
        var positions = await client.GetFromJsonAsync<List<TrackingPositionResponse>>("/api/v1/tracking/positions");
        Assert.Empty(positions!);
        var metrics = await client.GetFromJsonAsync<TrackingMetricsResponse>("/api/v1/tracking/metrics");
        Assert.Equal(0, metrics!.HistoryPointCount);
        Assert.Equal(0, metrics.AcceptedCount);
    }

    [RequiresDockerFact]
    public async Task RetentionPurgeQueryPlanUsesRecordedAtUtcIndex()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        var scenario = await LoadScenarioAsync(client, "northwind", 3);
        var organizationId = scenario.OrganizationId;
        var vehicleId = scenario.Vehicles[0].VehicleId;

        await using (var connection = await factory.OpenConnectionAsync())
        {
            await using var insert = (SqlCommand)connection.CreateCommand();
            insert.CommandText = """
                INSERT INTO TelemetryPoints
                    (Id, OrganizationId, VehicleId, DeviceId, EventId, RecordedAtUtc, IngestedAtUtc, Latitude, Longitude, SpeedKph, HeadingDegrees, SequenceNumber, AccuracyMeters, Source, QualityScore, AnomalyFlags, CreatedAtUtc)
                SELECT TOP (20000)
                    NEWID(), @organizationId, @vehicleId, 'NW-GPS-100',
                    'plan-' + CONVERT(nvarchar(20), ROW_NUMBER() OVER (ORDER BY (SELECT NULL))),
                    DATEADD(SECOND, -CONVERT(int, ROW_NUMBER() OVER (ORDER BY (SELECT NULL))), @now),
                    @now, 48.5, 9.2, 30, 90, 1, 5, 'plan', 100, '', @now
                FROM sys.all_objects AS a CROSS JOIN sys.all_objects AS b;
                UPDATE STATISTICS TelemetryPoints;
                """;
            insert.Parameters.AddWithValue("@organizationId", organizationId);
            insert.Parameters.AddWithValue("@vehicleId", vehicleId);
            insert.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow);
            await insert.ExecuteNonQueryAsync();
        }

        var plan = new System.Text.StringBuilder();
        await using (var connection = await factory.OpenConnectionAsync())
        {
            await using (var toggle = connection.CreateCommand())
            {
                toggle.CommandText = "SET STATISTICS XML ON";
                await toggle.ExecuteNonQueryAsync();
            }

            await using (var query = (SqlCommand)connection.CreateCommand())
            {
                query.CommandText = "SELECT TOP (1000) Id FROM TelemetryPoints WHERE RecordedAtUtc < @cutoff ORDER BY RecordedAtUtc";
                query.Parameters.AddWithValue("@cutoff", DateTimeOffset.UtcNow);
                await using var reader = await query.ExecuteReaderAsync();
                do
                {
                    while (await reader.ReadAsync())
                    {
                        for (var ordinal = 0; ordinal < reader.FieldCount; ordinal++)
                        {
                            var value = reader.GetValue(ordinal)?.ToString();
                            if (value is not null && value.Contains("<ShowPlanXML", StringComparison.Ordinal))
                            {
                                plan.Append(value);
                            }
                        }
                    }
                }
                while (await reader.NextResultAsync());
            }

            await using (var toggle = connection.CreateCommand())
            {
                toggle.CommandText = "SET STATISTICS XML OFF";
                await toggle.ExecuteNonQueryAsync();
            }
        }

        var planXml = plan.ToString();
        Assert.NotEmpty(planXml);
        Assert.Contains("IX_TelemetryPoints_RecordedAtUtc", planXml, StringComparison.Ordinal);
    }

    private static async Task<TrackingScenarioResponse> LoadScenarioAsync(HttpClient client, string slug, int maxVehicles)
    {
        var reset = await client.PostAsync($"/api/internal/v1/tracking/scenarios/{slug}/reset", null);
        reset.EnsureSuccessStatusCode();
        return (await client.GetFromJsonAsync<TrackingScenarioResponse>($"/api/internal/v1/tracking/scenarios/{slug}?maxVehicles={maxVehicles}"))!;
    }

    private static Task<HttpResponseMessage> PostTelemetryAsync(
        HttpClient client,
        TrackingScenarioResponse scenario,
        TrackingScenarioVehicleResponse vehicle,
        string eventId,
        DateTimeOffset recordedAtUtc,
        long sequence) =>
        client.PostAsJsonAsync("/api/internal/v1/tracking/events", new IngestTelemetryRequest(
            scenario.OrganizationId,
            vehicle.VehicleId,
            vehicle.DeviceId,
            eventId,
            recordedAtUtc,
            48.7758,
            9.1829,
            42,
            90,
            sequence,
            5,
            "reliability-test"));

    private sealed class RestartedApiFactory(string connectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:FleetOps"] = connectionString,
                    ["Jwt:Issuer"] = "FleetOps.Tests",
                    ["Jwt:Audience"] = "FleetOps.Tests.Web",
                    ["Jwt:SigningKey"] = "FleetOps_Tests_Signing_Key_12345678901234567890",
                    ["Jwt:TokenLifetimeMinutes"] = "60",
                    ["FLEETOPS_WEB_URL"] = "http://localhost:5173",
                    ["Bootstrap:SeedDemoData"] = "true",
                    ["Security:LoginPermitLimit"] = "100",
                    ["Integrations:RetryBaseDelaySeconds"] = "0",
                    ["Integrations:MaxWebhookAttempts"] = "3"
                }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<FleetOpsDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<FleetOpsDbContext>>();
                services.AddDbContext<FleetOpsDbContext>(options => options.UseSqlServer(connectionString));
            });
        }
    }
}
