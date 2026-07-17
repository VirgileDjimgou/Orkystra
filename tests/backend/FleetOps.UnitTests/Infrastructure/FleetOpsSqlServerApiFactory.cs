using System.Data.Common;
using System.Globalization;
using FleetOps.Api;
using FleetOps.Api.Tracking;
using FleetOps.Infrastructure.Identity;
using FleetOps.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;
using Xunit;

namespace FleetOps.UnitTests.Infrastructure;

public sealed class FleetOpsSqlServerApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _databaseName = $"FleetOpsSprint11_{Guid.NewGuid():N}";
    private readonly MsSqlContainer _sqlContainer = new MsSqlBuilder().Build();

    public string DatabaseName => _databaseName;

    public string ConnectionString
    {
        get
        {
            var builder = new SqlConnectionStringBuilder(_sqlContainer.GetConnectionString())
            {
                InitialCatalog = _databaseName,
                TrustServerCertificate = true
            };

            return builder.ConnectionString;
        }
    }

    public string MasterConnectionString
    {
        get
        {
            var builder = new SqlConnectionStringBuilder(_sqlContainer.GetConnectionString())
            {
                InitialCatalog = "master",
                TrustServerCertificate = true
            };

            return builder.ConnectionString;
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration(configuration =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:FleetOps"] = ConnectionString,
                ["Jwt:Issuer"] = "FleetOps.Tests",
                ["Jwt:Audience"] = "FleetOps.Tests.Web",
                ["Jwt:SigningKey"] = "FleetOps_Tests_Signing_Key_12345678901234567890",
                ["Jwt:TokenLifetimeMinutes"] = "60",
                ["FLEETOPS_WEB_URL"] = "http://localhost:5173",
                ["Bootstrap:SeedDemoData"] = "true",
                ["Security:LoginPermitLimit"] = "100",
                ["Integrations:RetryBaseDelaySeconds"] = "0",
                ["Integrations:MaxWebhookAttempts"] = "3"
            });
        });
    }

    public async Task InitializeAsync()
    {
        await _sqlContainer.StartAsync();
        using var _ = CreateClient();
        await ResetDatabaseAsync();
    }

    public async Task ResetDatabaseAsync()
    {
        await DropDatabaseIfExistsAsync(_databaseName);

        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FleetOpsDbContext>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var metricsStore = scope.ServiceProvider.GetRequiredService<TrackingMetricsStore>();

        metricsStore.ResetAll();
        await FleetOpsSeedData.EnsureSeededAsync(
            dbContext,
            roleManager,
            userManager,
            new BootstrapOptions { SeedDemoData = true },
            CancellationToken.None);
    }

    public async Task ExecuteMasterNonQueryAsync(string sql, params SqlParameter[] parameters)
    {
        await using var connection = new SqlConnection(MasterConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddRange(parameters);
        await command.ExecuteNonQueryAsync();
    }

    public static async Task<T> ExecuteScalarAsync<T>(string connectionString, string sql, params SqlParameter[] parameters)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddRange(parameters);
        var result = await command.ExecuteScalarAsync();
        return result is null or DBNull
            ? default!
            : (T)Convert.ChangeType(result, typeof(T), CultureInfo.InvariantCulture);
    }

    public async Task<Dictionary<string, string>> ReadDatabaseFileNamesAsync(string databaseName)
    {
        await using var connection = new SqlConnection(MasterConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT name, type_desc
            FROM sys.master_files
            WHERE database_id = DB_ID(@databaseName);
            """;
        command.Parameters.Add(new SqlParameter("@databaseName", databaseName));

        await using var reader = await command.ExecuteReaderAsync();
        var results = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        while (await reader.ReadAsync())
        {
            results[reader.GetString(1)] = reader.GetString(0);
        }

        return results;
    }

    public async Task<DbConnection> OpenConnectionAsync(string? databaseName = null)
    {
        var builder = new SqlConnectionStringBuilder(_sqlContainer.GetConnectionString())
        {
            InitialCatalog = databaseName ?? _databaseName,
            TrustServerCertificate = true
        };

        var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    public new async Task DisposeAsync()
    {
        await _sqlContainer.DisposeAsync();
    }

    private async Task DropDatabaseIfExistsAsync(string databaseName)
    {
        const string sql = """
            IF DB_ID(@databaseName) IS NOT NULL
            BEGIN
                EXEC ('ALTER DATABASE [' + @databaseName + '] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;');
                EXEC ('DROP DATABASE [' + @databaseName + '];');
            END
            """;

        await ExecuteMasterNonQueryAsync(sql, new SqlParameter("@databaseName", databaseName));
    }
}
