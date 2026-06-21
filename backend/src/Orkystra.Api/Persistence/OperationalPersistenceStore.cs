using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace Orkystra.Api.Persistence;

public sealed class OperationalPersistenceStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly string _databasePath;
    private readonly SemaphoreSlim _initializationGate = new(1, 1);
    private volatile bool _initialized;

    public OperationalPersistenceStore(IOptions<OperationalPersistenceOptions> options, string contentRootPath)
    {
        var configuredPath = options.Value.DatabasePath;
        _databasePath = Path.GetFullPath(Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(contentRootPath, configuredPath));
    }

    public async Task UpsertProjectionAsync<TPayload>(
        string tenantId,
        string projectionName,
        string projectionKey,
        string source,
        TPayload payload,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectionName);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectionKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        await EnsureInitializedAsync(cancellationToken);

        var payloadJson = JsonSerializer.Serialize(payload, SerializerOptions);

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO projection_snapshots (
                tenant_id,
                projection_name,
                projection_key,
                source,
                captured_at_utc,
                payload_json
            )
            VALUES (
                $tenantId,
                $projectionName,
                $projectionKey,
                $source,
                $capturedAtUtc,
                $payloadJson
            )
            ON CONFLICT(tenant_id, projection_name, projection_key)
            DO UPDATE SET
                source = excluded.source,
                captured_at_utc = excluded.captured_at_utc,
                payload_json = excluded.payload_json;
            """;
        command.Parameters.AddWithValue("$tenantId", tenantId);
        command.Parameters.AddWithValue("$projectionName", projectionName);
        command.Parameters.AddWithValue("$projectionKey", projectionKey);
        command.Parameters.AddWithValue("$source", source);
        command.Parameters.AddWithValue("$capturedAtUtc", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$payloadJson", payloadJson);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task AppendWorkflowRunAsync<TPayload>(
        string tenantId,
        string workflowKind,
        string subjectKey,
        string? scenarioId,
        string source,
        string status,
        TPayload payload,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowKind);
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(status);

        await EnsureInitializedAsync(cancellationToken);

        var payloadJson = JsonSerializer.Serialize(payload, SerializerOptions);

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO workflow_runs (
                tenant_id,
                workflow_kind,
                subject_key,
                scenario_id,
                source,
                status,
                created_at_utc,
                payload_json
            )
            VALUES (
                $tenantId,
                $workflowKind,
                $subjectKey,
                $scenarioId,
                $source,
                $status,
                $createdAtUtc,
                $payloadJson
            );
            """;
        command.Parameters.AddWithValue("$tenantId", tenantId);
        command.Parameters.AddWithValue("$workflowKind", workflowKind);
        command.Parameters.AddWithValue("$subjectKey", subjectKey);
        command.Parameters.AddWithValue("$scenarioId", (object?)scenarioId ?? DBNull.Value);
        command.Parameters.AddWithValue("$source", source);
        command.Parameters.AddWithValue("$status", status);
        command.Parameters.AddWithValue("$createdAtUtc", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$payloadJson", payloadJson);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<PersistedProjectionSnapshot>> ReadProjectionSnapshotsAsync(
        string tenantId,
        string? projectionName,
        int count,
        CancellationToken cancellationToken = default)
    {
        if (count <= 0)
        {
            return [];
        }

        await EnsureInitializedAsync(cancellationToken);

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT tenant_id, projection_name, projection_key, source, captured_at_utc, payload_json
            FROM projection_snapshots
            WHERE tenant_id = $tenantId
              AND ($projectionName IS NULL OR projection_name = $projectionName)
            ORDER BY captured_at_utc DESC, projection_name ASC, projection_key ASC
            LIMIT $count;
            """;
        command.Parameters.AddWithValue("$tenantId", tenantId);
        command.Parameters.AddWithValue("$projectionName", (object?)projectionName ?? DBNull.Value);
        command.Parameters.AddWithValue("$count", count);

        var snapshots = new List<PersistedProjectionSnapshot>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            snapshots.Add(new PersistedProjectionSnapshot(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                DateTimeOffset.Parse(reader.GetString(4)),
                reader.GetString(5)));
        }

        return snapshots;
    }

    public async Task<IReadOnlyCollection<PersistedWorkflowRun>> ReadWorkflowRunsAsync(
        string tenantId,
        string? workflowKind,
        int count,
        CancellationToken cancellationToken = default)
    {
        if (count <= 0)
        {
            return [];
        }

        await EnsureInitializedAsync(cancellationToken);

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, tenant_id, workflow_kind, subject_key, scenario_id, source, status, created_at_utc, payload_json
            FROM workflow_runs
            WHERE tenant_id = $tenantId
              AND ($workflowKind IS NULL OR workflow_kind = $workflowKind)
            ORDER BY created_at_utc DESC, id DESC
            LIMIT $count;
            """;
        command.Parameters.AddWithValue("$tenantId", tenantId);
        command.Parameters.AddWithValue("$workflowKind", (object?)workflowKind ?? DBNull.Value);
        command.Parameters.AddWithValue("$count", count);

        var runs = new List<PersistedWorkflowRun>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            runs.Add(new PersistedWorkflowRun(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                DateTimeOffset.Parse(reader.GetString(7)),
                reader.GetString(8)));
        }

        return runs;
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized)
        {
            return;
        }

        await _initializationGate.WaitAsync(cancellationToken);
        try
        {
            if (_initialized)
            {
                return;
            }

            var directory = Path.GetDirectoryName(_databasePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                CREATE TABLE IF NOT EXISTS projection_snapshots (
                    tenant_id TEXT NOT NULL,
                    projection_name TEXT NOT NULL,
                    projection_key TEXT NOT NULL,
                    source TEXT NOT NULL,
                    captured_at_utc TEXT NOT NULL,
                    payload_json TEXT NOT NULL,
                    PRIMARY KEY (tenant_id, projection_name, projection_key)
                );

                CREATE TABLE IF NOT EXISTS workflow_runs (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    tenant_id TEXT NOT NULL,
                    workflow_kind TEXT NOT NULL,
                    subject_key TEXT NOT NULL,
                    scenario_id TEXT NULL,
                    source TEXT NOT NULL,
                    status TEXT NOT NULL,
                    created_at_utc TEXT NOT NULL,
                    payload_json TEXT NOT NULL
                );

                CREATE INDEX IF NOT EXISTS ix_workflow_runs_tenant_kind_created
                    ON workflow_runs (tenant_id, workflow_kind, created_at_utc DESC);
                """;

            await command.ExecuteNonQueryAsync(cancellationToken);
            _initialized = true;
        }
        finally
        {
            _initializationGate.Release();
        }
    }

    private SqliteConnection CreateConnection()
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        };

        return new SqliteConnection(builder.ToString());
    }
}
