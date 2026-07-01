using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using Npgsql;
using Orkystra.Api.Persistence;

namespace Orkystra.Api.Eventing;

public sealed class EventOutboxEntry
{
    public long Id { get; init; }
    public string MessageId { get; init; } = string.Empty;
    public string EventType { get; init; } = string.Empty;
    public string Topic { get; init; } = string.Empty;
    public string PayloadJson { get; init; } = string.Empty;
    public string Status { get; init; } = "pending";
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset? PublishedAtUtc { get; init; }
    public string? ErrorMessage { get; init; }
}

public sealed class EventOutboxStore : IDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly OperationalPersistenceOptions _options;
    private readonly string _contentRootPath;
    private readonly SemaphoreSlim _initializationGate = new(1, 1);
    private volatile bool _initialized;
    private SqliteConnection? _sharedSqliteConnection;

    public EventOutboxStore(IOptions<OperationalPersistenceOptions> options, string contentRootPath)
    {
        _options = options.Value;
        _contentRootPath = contentRootPath;
    }

    public void Dispose()
    {
        _sharedSqliteConnection?.Dispose();
    }

    public async Task<long> RecordPendingAsync(string messageId, string eventType, string topic, object payload, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        var payloadJson = JsonSerializer.Serialize(payload, SerializerOptions);

        if (_options.Provider == "postgres")
        {
            await using var connection = new NpgsqlConnection(_options.ConnectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                INSERT INTO outbox_messages (message_id, event_type, topic, payload_json, status, created_at_utc)
                VALUES ($1, $2, $3, $4, 'pending', $5)
                RETURNING id;
                """;
            command.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Text, messageId);
            command.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Text, eventType);
            command.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Text, topic);
            command.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Text, payloadJson);
            command.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.TimestampTz, DateTimeOffset.UtcNow);
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt64(result);
        }

        var sqliteConnection = await GetSqliteConnectionAsync();
        await using var sqliteCommand = sqliteConnection.CreateCommand();
        sqliteCommand.CommandText =
            """
            INSERT INTO outbox_messages (message_id, event_type, topic, payload_json, status, created_at_utc)
            VALUES ($messageId, $eventType, $topic, $payloadJson, 'pending', $createdAtUtc);
            SELECT last_insert_rowid();
            """;
        sqliteCommand.Parameters.AddWithValue("$messageId", messageId);
        sqliteCommand.Parameters.AddWithValue("$eventType", eventType);
        sqliteCommand.Parameters.AddWithValue("$topic", topic);
        sqliteCommand.Parameters.AddWithValue("$payloadJson", payloadJson);
        sqliteCommand.Parameters.AddWithValue("$createdAtUtc", DateTimeOffset.UtcNow.ToString("O"));
        var sqliteResult = await sqliteCommand.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(sqliteResult);
    }

    public async Task MarkPublishedAsync(long entryId, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        if (_options.Provider == "postgres")
        {
            await using var connection = new NpgsqlConnection(_options.ConnectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "UPDATE outbox_messages SET status = 'published', published_at_utc = $1 WHERE id = $2";
            command.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.TimestampTz, DateTimeOffset.UtcNow);
            command.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Bigint, entryId);
            await command.ExecuteNonQueryAsync(cancellationToken);
            return;
        }

        var sqliteConnection = await GetSqliteConnectionAsync();
        await using var sqliteCommand = sqliteConnection.CreateCommand();
        sqliteCommand.CommandText = "UPDATE outbox_messages SET status = 'published', published_at_utc = $publishedAtUtc WHERE id = $id";
        sqliteCommand.Parameters.AddWithValue("$publishedAtUtc", DateTimeOffset.UtcNow.ToString("O"));
        sqliteCommand.Parameters.AddWithValue("$id", entryId);
        await sqliteCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task MarkFailedAsync(long entryId, string errorMessage, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        if (_options.Provider == "postgres")
        {
            await using var connection = new NpgsqlConnection(_options.ConnectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "UPDATE outbox_messages SET status = 'failed', error_message = $1 WHERE id = $2";
            command.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Text, errorMessage);
            command.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Bigint, entryId);
            await command.ExecuteNonQueryAsync(cancellationToken);
            return;
        }

        var sqliteConnection = await GetSqliteConnectionAsync();
        await using var sqliteCommand = sqliteConnection.CreateCommand();
        sqliteCommand.CommandText = "UPDATE outbox_messages SET status = 'failed', error_message = $errorMessage WHERE id = $id";
        sqliteCommand.Parameters.AddWithValue("$errorMessage", errorMessage);
        sqliteCommand.Parameters.AddWithValue("$id", entryId);
        await sqliteCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<EventOutboxEntry>> GetPendingEntriesAsync(int count = 50, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        if (_options.Provider == "postgres")
        {
            await using var connection = new NpgsqlConnection(_options.ConnectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT id, message_id, event_type, topic, payload_json, status, created_at_utc, published_at_utc, error_message
                FROM outbox_messages
                WHERE status IN ('pending', 'failed')
                ORDER BY created_at_utc ASC
                LIMIT $1;
                """;
            command.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Integer, count);
            return await ReadEntriesPostgresAsync(command, cancellationToken);
        }

        var sqliteConnection = await GetSqliteConnectionAsync();
        await using var sqliteCommand = sqliteConnection.CreateCommand();
        sqliteCommand.CommandText =
            """
            SELECT id, message_id, event_type, topic, payload_json, status, created_at_utc, published_at_utc, error_message
            FROM outbox_messages
            WHERE status IN ('pending', 'failed')
            ORDER BY created_at_utc ASC
            LIMIT $count;
            """;
        sqliteCommand.Parameters.AddWithValue("$count", count);
        return await ReadEntriesSqliteAsync(sqliteCommand, cancellationToken);
    }

    public async Task<IReadOnlyCollection<EventOutboxEntry>> GetRecentEntriesAsync(int count = 50, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        if (_options.Provider == "postgres")
        {
            await using var connection = new NpgsqlConnection(_options.ConnectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT id, message_id, event_type, topic, payload_json, status, created_at_utc, published_at_utc, error_message
                FROM outbox_messages
                ORDER BY created_at_utc DESC
                LIMIT $1;
                """;
            command.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Integer, count);
            return await ReadEntriesPostgresAsync(command, cancellationToken);
        }

        var sqliteConnection = await GetSqliteConnectionAsync();
        await using var sqliteCommand = sqliteConnection.CreateCommand();
        sqliteCommand.CommandText =
            """
            SELECT id, message_id, event_type, topic, payload_json, status, created_at_utc, published_at_utc, error_message
            FROM outbox_messages
            ORDER BY created_at_utc DESC
            LIMIT $count;
            """;
        sqliteCommand.Parameters.AddWithValue("$count", count);
        return await ReadEntriesSqliteAsync(sqliteCommand, cancellationToken);
    }

    private static async Task<IReadOnlyCollection<EventOutboxEntry>> ReadEntriesPostgresAsync(NpgsqlCommand command, CancellationToken cancellationToken)
    {
        var entries = new List<EventOutboxEntry>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            entries.Add(new EventOutboxEntry
            {
                Id = reader.GetInt64(0),
                MessageId = reader.GetString(1),
                EventType = reader.GetString(2),
                Topic = reader.GetString(3),
                PayloadJson = reader.GetString(4),
                Status = reader.GetString(5),
                CreatedAtUtc = reader.GetFieldValue<DateTimeOffset>(6),
                PublishedAtUtc = reader.IsDBNull(7) ? null : reader.GetFieldValue<DateTimeOffset>(7),
                ErrorMessage = reader.IsDBNull(8) ? null : reader.GetString(8)
            });
        }
        return entries;
    }

    private static async Task<IReadOnlyCollection<EventOutboxEntry>> ReadEntriesSqliteAsync(SqliteCommand command, CancellationToken cancellationToken)
    {
        var entries = new List<EventOutboxEntry>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            entries.Add(new EventOutboxEntry
            {
                Id = reader.GetInt64(0),
                MessageId = reader.GetString(1),
                EventType = reader.GetString(2),
                Topic = reader.GetString(3),
                PayloadJson = reader.GetString(4),
                Status = reader.GetString(5),
                CreatedAtUtc = DateTimeOffset.Parse(reader.GetString(6)),
                PublishedAtUtc = reader.IsDBNull(7) ? null : DateTimeOffset.Parse(reader.GetString(7)),
                ErrorMessage = reader.IsDBNull(8) ? null : reader.GetString(8)
            });
        }
        return entries;
    }

    private async Task<SqliteConnection> GetSqliteConnectionAsync()
    {
        if (_sharedSqliteConnection is null)
        {
            _sharedSqliteConnection = CreateSqliteConnection();
            await _sharedSqliteConnection.OpenAsync();
        }
        return _sharedSqliteConnection;
    }

    private SqliteConnection CreateSqliteConnection()
    {
        var databasePath = ResolveDatabasePath();
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        };

        return new SqliteConnection(builder.ToString());
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

            if (_options.Provider == "postgres")
            {
                await using var connection = new NpgsqlConnection(_options.ConnectionString);
                await connection.OpenAsync(cancellationToken);
                await using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    CREATE TABLE IF NOT EXISTS outbox_messages (
                        id BIGSERIAL PRIMARY KEY,
                        message_id TEXT NOT NULL,
                        event_type TEXT NOT NULL,
                        topic TEXT NOT NULL,
                        payload_json TEXT NOT NULL,
                        status TEXT NOT NULL DEFAULT 'pending',
                        created_at_utc TIMESTAMPTZ NOT NULL,
                        published_at_utc TIMESTAMPTZ NULL,
                        error_message TEXT NULL
                    );

                    CREATE INDEX IF NOT EXISTS ix_outbox_messages_status_created
                        ON outbox_messages (status, created_at_utc ASC);
                    """;
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            else
            {
                var databasePath = ResolveDatabasePath();
                var directory = Path.GetDirectoryName(databasePath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                await using var connection = CreateSqliteConnection();
                await connection.OpenAsync(cancellationToken);
                await using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    CREATE TABLE IF NOT EXISTS outbox_messages (
                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                        message_id TEXT NOT NULL,
                        event_type TEXT NOT NULL,
                        topic TEXT NOT NULL,
                        payload_json TEXT NOT NULL,
                        status TEXT NOT NULL DEFAULT 'pending',
                        created_at_utc TEXT NOT NULL,
                        published_at_utc TEXT NULL,
                        error_message TEXT NULL
                    );

                    CREATE INDEX IF NOT EXISTS ix_outbox_messages_status_created
                        ON outbox_messages (status, created_at_utc ASC);
                    """;
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            _initialized = true;
        }
        finally
        {
            _initializationGate.Release();
        }
    }

    private string ResolveDatabasePath()
    {
        var configuredPath = _options.DatabasePath;
        return Path.GetFullPath(Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(_contentRootPath, configuredPath));
    }
}
