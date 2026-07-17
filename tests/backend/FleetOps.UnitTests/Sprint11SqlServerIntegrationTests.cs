using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using FleetOps.Api.Dispatch;
using FleetOps.Core.Modules.Dispatch;
using FleetOps.Core.Modules.Fleet;
using FleetOps.Core.Modules.Integrations;
using FleetOps.Infrastructure.Identity;
using FleetOps.Infrastructure.Persistence;
using FleetOps.UnitTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FleetOps.UnitTests;

[Trait("Category", "SqlServer")]
public sealed class Sprint11SqlServerIntegrationTests(FleetOpsSqlServerApiFactory factory)
    : IClassFixture<FleetOpsSqlServerApiFactory>
{
    [RequiresDockerFact]
    public async Task EmptyDatabaseIsCreatedByMigrationsOnly()
    {
        await factory.ResetDatabaseAsync();

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FleetOpsDbContext>();

        Assert.True(dbContext.Database.IsRelational());
        Assert.Empty(await dbContext.Database.GetPendingMigrationsAsync());

        var appliedMigrations = await dbContext.Database.GetAppliedMigrationsAsync();
        Assert.NotEmpty(appliedMigrations);

        var requiredTables = await FleetOpsSqlServerApiFactory.ExecuteScalarAsync<int>(
            factory.ConnectionString,
            """
            SELECT COUNT(*)
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_NAME IN ('__EFMigrationsHistory', 'Organizations', 'Missions', 'IntegrationOutboxMessages');
            """);

        Assert.Equal(4, requiredTables);
    }

    [RequiresDockerFact]
    public async Task SqlServerEnforcesTenantScopedUniquenessAndOptimisticConcurrency()
    {
        await factory.ResetDatabaseAsync();

        Guid northwindId;
        Guid southridgeId;

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FleetOpsDbContext>();
            northwindId = await dbContext.Organizations
                .Where(x => x.Slug == "northwind")
                .Select(x => x.Id)
                .SingleAsync();
            southridgeId = await dbContext.Organizations
                .Where(x => x.Slug == "southridge")
                .Select(x => x.Id)
                .SingleAsync();

            dbContext.Vehicles.Add(new Vehicle(southridgeId, "NW-100", "Southridge shared registration"));
            await dbContext.SaveChangesAsync();

            dbContext.Vehicles.Add(new Vehicle(northwindId, "NW-100", "Northwind duplicate registration"));
            await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
        }

        await using var firstScope = factory.Services.CreateAsyncScope();
        await using var secondScope = factory.Services.CreateAsyncScope();
        var firstContext = firstScope.ServiceProvider.GetRequiredService<FleetOpsDbContext>();
        var secondContext = secondScope.ServiceProvider.GetRequiredService<FleetOpsDbContext>();

        var firstVehicle = await firstContext.Vehicles.SingleAsync(
            x => x.OrganizationId == northwindId && x.RegistrationNumber == "NW-100");
        var staleVehicle = await secondContext.Vehicles.SingleAsync(
            x => x.OrganizationId == northwindId && x.RegistrationNumber == "NW-100");

        firstVehicle.Rename("Northwind Dispatch Van Prime");
        await firstContext.SaveChangesAsync();

        staleVehicle.Rename("Northwind Stale Rename");
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => secondContext.SaveChangesAsync());
    }

    [RequiresDockerFact]
    public async Task BackupRestorePreservesBusinessChecksum()
    {
        await factory.ResetDatabaseAsync();
        await SeedRecoveryScenarioAsync();

        var before = await BuildSnapshotAsync(factory.ConnectionString);
        var restoredDatabaseName = $"{factory.DatabaseName}_restored";

        await RestoreDatabaseAsync(restoredDatabaseName);

        var restoredConnectionString = new SqlConnectionStringBuilder(factory.ConnectionString)
        {
            InitialCatalog = restoredDatabaseName,
            TrustServerCertificate = true
        }.ConnectionString;

        var after = await BuildSnapshotAsync(restoredConnectionString);

        Assert.Equal(before.Hash, after.Hash);
        Assert.Equal(before.MissionCount, after.MissionCount);
        Assert.Equal(before.OutboxCount, after.OutboxCount);
        Assert.Equal(before.VehicleCount, after.VehicleCount);
        Assert.Equal(before.TimelineCount, after.TimelineCount);
    }

    private async Task SeedRecoveryScenarioAsync()
    {
        using var client = factory.CreateClient();
        var login = await client.LoginAsync("operator@northwind.local", "Operator123!");
        client.SetBearer(login.AccessToken);

        var mission = await CreateMissionAsync(client, "NW-M-SQL-100", "SQL recovery verification route");
        var assigned = await ProgressMissionToAssignedAsync(client, mission);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FleetOpsDbContext>();

        var organization = await dbContext.Organizations.SingleAsync(x => x.Slug == "northwind");
        var endpoint = new WebhookEndpoint(
            organization.Id,
            "Sprint 11 recovery hook",
            "mission.updated",
            "https://example.test/webhooks/fleetops",
            "sprint11-secret",
            isSandbox: false);

        dbContext.WebhookEndpoints.Add(endpoint);
        await dbContext.SaveChangesAsync();

        dbContext.IntegrationOutboxMessages.Add(new IntegrationOutboxMessage(
            organization.Id,
            endpoint.Id,
            "mission.updated",
            "Mission",
            assigned.Id.ToString(),
            $$"""{"reference":"{{assigned.Reference}}","status":"{{assigned.Status}}"}""",
            DateTimeOffset.UtcNow));

        await dbContext.SaveChangesAsync();
    }

    private async Task RestoreDatabaseAsync(string restoredDatabaseName)
    {
        const string backupDirectory = "/var/opt/mssql/data";
        var backupPath = $"{backupDirectory}/{factory.DatabaseName}.bak";
        var logicalNames = await factory.ReadDatabaseFileNamesAsync(factory.DatabaseName);
        var logicalData = logicalNames["ROWS"];
        var logicalLog = logicalNames["LOG"];
        var restoredDataPath = $"{backupDirectory}/{restoredDatabaseName}.mdf";
        var restoredLogPath = $"{backupDirectory}/{restoredDatabaseName}_log.ldf";

        await factory.ExecuteMasterNonQueryAsync(
            $"BACKUP DATABASE [{factory.DatabaseName}] TO DISK = '{backupPath}' WITH INIT, CHECKSUM;");

        await factory.ExecuteMasterNonQueryAsync(
            $"""
            IF DB_ID('{restoredDatabaseName}') IS NOT NULL
            BEGIN
                ALTER DATABASE [{restoredDatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                DROP DATABASE [{restoredDatabaseName}];
            END;
            RESTORE DATABASE [{restoredDatabaseName}]
            FROM DISK = '{backupPath}'
            WITH
                MOVE '{logicalData}' TO '{restoredDataPath}',
                MOVE '{logicalLog}' TO '{restoredLogPath}',
                REPLACE,
                RECOVERY,
                CHECKSUM;
            """);
    }

    private static async Task<BusinessSnapshot> BuildSnapshotAsync(string connectionString)
    {
        var organizations = await ReadValuesAsync(
            connectionString,
            "SELECT Slug FROM Organizations ORDER BY Slug;");
        var missions = await ReadValuesAsync(
            connectionString,
            "SELECT Reference + ':' + CAST(Status AS nvarchar(10)) FROM Missions ORDER BY Reference;");
        var vehicles = await ReadValuesAsync(
            connectionString,
            "SELECT RegistrationNumber FROM Vehicles ORDER BY RegistrationNumber;");
        var outbox = await ReadValuesAsync(
            connectionString,
            "SELECT EventType + ':' + AggregateId FROM IntegrationOutboxMessages ORDER BY EventType, AggregateId;");
        var timelines = await ReadValuesAsync(
            connectionString,
            "SELECT Description FROM MissionTimelineEvents ORDER BY OccurredAtUtc, Description;");

        var payload = string.Join(
            "|",
            [
                string.Join(",", organizations),
                string.Join(",", missions),
                string.Join(",", vehicles),
                string.Join(",", outbox),
                string.Join(",", timelines),
            ]);

        return new BusinessSnapshot(
            ComputeSha256(payload),
            organizations.Count,
            missions.Count,
            vehicles.Count,
            outbox.Count,
            timelines.Count);
    }

    private static async Task<MissionDetailResponse> ProgressMissionToAssignedAsync(HttpClient client, MissionDetailResponse mission)
    {
        var plannedResponse = await client.PostAsJsonAsync(
            $"/api/v1/dispatch/missions/{mission.Id}/status",
            new TransitionMissionStatusRequest(MissionStatus.Planned, mission.RowVersion));
        var planned = await plannedResponse.Content.ReadFromJsonAsync<MissionDetailResponse>();
        Assert.Equal(HttpStatusCode.OK, plannedResponse.StatusCode);

        var drivers = await client.GetFromJsonAsync<List<DriverCandidate>>("/api/v1/fleet/drivers");
        var vehicles = await client.GetFromJsonAsync<List<VehicleCandidate>>("/api/v1/fleet/vehicles");
        var driver = Assert.Single(drivers!, x => x.LicenseNumber == "NW-DL-001");
        var vehicle = Assert.Single(vehicles!, x => x.RegistrationNumber == "NW-100");

        var assignmentResponse = await client.PutAsJsonAsync(
            $"/api/v1/dispatch/missions/{planned!.Id}/assignment",
            new SetMissionAssignmentRequest(driver.Id, vehicle.Id, planned.RowVersion));
        var assigned = await assignmentResponse.Content.ReadFromJsonAsync<MissionDetailResponse>();
        Assert.Equal(HttpStatusCode.OK, assignmentResponse.StatusCode);

        var assignedStatusResponse = await client.PostAsJsonAsync(
            $"/api/v1/dispatch/missions/{assigned!.Id}/status",
            new TransitionMissionStatusRequest(MissionStatus.Assigned, assigned.RowVersion));
        var progressed = await assignedStatusResponse.Content.ReadFromJsonAsync<MissionDetailResponse>();
        Assert.Equal(HttpStatusCode.OK, assignedStatusResponse.StatusCode);
        return progressed!;
    }

    private static async Task<MissionDetailResponse> CreateMissionAsync(HttpClient client, string reference, string title)
    {
        var start = DateTimeOffset.UtcNow.AddHours(2);
        var response = await client.PostAsJsonAsync(
            "/api/v1/dispatch/missions",
            new CreateMissionRequest(
                reference,
                title,
                start,
                start.AddHours(2),
                [
                    new MissionStopRequest(1, "Depot", "1 Dispatch Way", start.AddMinutes(30)),
                    new MissionStopRequest(2, "Customer", "22 Fleet Street", start.AddMinutes(90)),
                ]));
        var mission = await response.Content.ReadFromJsonAsync<MissionDetailResponse>();
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return mission!;
    }

    private static async Task<List<string>> ReadValuesAsync(string connectionString, string sql)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        var values = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            values.Add(reader.GetString(0));
        }

        return values;
    }

    private static string ComputeSha256(string payload)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes);
    }

    private sealed record DriverCandidate(Guid Id, string FullName, string LicenseNumber, string? PhoneNumber, bool IsActive, long RowVersion);

    private sealed record VehicleCandidate(Guid Id, string RegistrationNumber, string DisplayName, bool IsActive, long RowVersion);

    private sealed record BusinessSnapshot(
        string Hash,
        int OrganizationCount,
        int MissionCount,
        int VehicleCount,
        int OutboxCount,
        int TimelineCount);
}
