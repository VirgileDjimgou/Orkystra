using System.Text.Json;
using Npgsql;
using Microsoft.Extensions.Options;

namespace Orkystra.Api.Persistence;

public sealed class PostgresOperationalPersistenceStore : IOperationalPersistenceStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly string _connectionString;
    private readonly SemaphoreSlim _initializationGate = new(1, 1);
    private volatile bool _initialized;

    public PostgresOperationalPersistenceStore(IOptions<OperationalPersistenceOptions> options)
    {
        _connectionString = options.Value.ConnectionString;
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

        await using var connection = new NpgsqlConnection(_connectionString);
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
                $1,
                $2,
                $3,
                $4,
                $5,
                $6
            )
            ON CONFLICT (tenant_id, projection_name, projection_key)
            DO UPDATE SET
                source = EXCLUDED.source,
                captured_at_utc = EXCLUDED.captured_at_utc,
                payload_json = EXCLUDED.payload_json;
            """;
        command.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Text, tenantId);
        command.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Text, projectionName);
        command.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Text, projectionKey);
        command.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Text, source);
        command.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.TimestampTz, DateTimeOffset.UtcNow);
        command.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Text, payloadJson);

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

        await using var connection = new NpgsqlConnection(_connectionString);
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
                $1,
                $2,
                $3,
                $4,
                $5,
                $6,
                $7,
                $8
            );
            """;
        command.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Text, tenantId);
        command.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Text, workflowKind);
        command.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Text, subjectKey);
        command.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Text, scenarioId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Text, source);
        command.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Text, status);
        command.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.TimestampTz, DateTimeOffset.UtcNow);
        command.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Text, payloadJson);

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

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT tenant_id, projection_name, projection_key, source, captured_at_utc, payload_json
            FROM projection_snapshots
            WHERE tenant_id = $1
              AND ($2 IS NULL OR projection_name = $2)
            ORDER BY captured_at_utc DESC, projection_name ASC, projection_key ASC
            LIMIT $3;
            """;
        command.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Text, tenantId);
        command.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Text, projectionName ?? (object)DBNull.Value);
        command.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Integer, count);

        var snapshots = new List<PersistedProjectionSnapshot>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            snapshots.Add(new PersistedProjectionSnapshot(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetFieldValue<DateTimeOffset>(4),
                reader.GetString(5)));
        }

        return snapshots;
    }

    public async Task<PersistedProjectionSnapshot?> ReadProjectionSnapshotAsync(
        string tenantId,
        string projectionName,
        string projectionKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectionName);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectionKey);

        await EnsureInitializedAsync(cancellationToken);

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT tenant_id, projection_name, projection_key, source, captured_at_utc, payload_json
            FROM projection_snapshots
            WHERE tenant_id = $1
              AND projection_name = $2
              AND projection_key = $3
            LIMIT 1;
            """;
        command.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Text, tenantId);
        command.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Text, projectionName);
        command.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Text, projectionKey);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new PersistedProjectionSnapshot(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetFieldValue<DateTimeOffset>(4),
            reader.GetString(5));
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

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, tenant_id, workflow_kind, subject_key, scenario_id, source, status, created_at_utc, payload_json
            FROM workflow_runs
            WHERE tenant_id = $1
              AND ($2 IS NULL OR workflow_kind = $2)
            ORDER BY created_at_utc DESC, id DESC
            LIMIT $3;
            """;
        command.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Text, tenantId);
        command.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Text, workflowKind ?? (object)DBNull.Value);
        command.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Integer, count);

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
                reader.GetFieldValue<DateTimeOffset>(7),
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

            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                CREATE TABLE IF NOT EXISTS projection_snapshots (
                    tenant_id TEXT NOT NULL,
                    projection_name TEXT NOT NULL,
                    projection_key TEXT NOT NULL,
                    source TEXT NOT NULL,
                    captured_at_utc TIMESTAMPTZ NOT NULL,
                    payload_json TEXT NOT NULL,
                    PRIMARY KEY (tenant_id, projection_name, projection_key)
                );

                CREATE TABLE IF NOT EXISTS workflow_runs (
                    id BIGSERIAL PRIMARY KEY,
                    tenant_id TEXT NOT NULL,
                    workflow_kind TEXT NOT NULL,
                    subject_key TEXT NOT NULL,
                    scenario_id TEXT NULL,
                    source TEXT NOT NULL,
                    status TEXT NOT NULL,
                    created_at_utc TIMESTAMPTZ NOT NULL,
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
}
