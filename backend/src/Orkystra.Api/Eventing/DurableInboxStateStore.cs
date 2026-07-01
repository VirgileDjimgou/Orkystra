using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using Npgsql;
using Orkystra.Application.Eventing;
using Orkystra.Api.Persistence;

namespace Orkystra.Api.Eventing;

public sealed class DurableInboxStateStore : IInboxStateStore, IDisposable
{
    private readonly OperationalPersistenceOptions _options;
    private readonly string _contentRootPath;
    private readonly SemaphoreSlim _initializationGate = new(1, 1);
    private volatile bool _initialized;
    private SqliteConnection? _sharedSqliteConnection;

    public DurableInboxStateStore(IOptions<OperationalPersistenceOptions> options, string contentRootPath)
    {
        _options = options.Value;
        _contentRootPath = contentRootPath;
    }

    public void Dispose()
    {
        _sharedSqliteConnection?.Dispose();
    }

    public async ValueTask<bool> HasProcessedAsync(string consumerName, Guid messageId, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        if (_options.Provider == "postgres")
        {
            return await HasProcessedPostgresAsync(consumerName, messageId, cancellationToken);
        }

        return await HasProcessedSqliteAsync(consumerName, messageId, cancellationToken);
    }

    public async ValueTask MarkProcessedAsync(string consumerName, Guid messageId, DateTimeOffset processedAt, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        if (_options.Provider == "postgres")
        {
            await MarkProcessedPostgresAsync(consumerName, messageId, processedAt, cancellationToken);
            return;
        }

        await MarkProcessedSqliteAsync(consumerName, messageId, processedAt, cancellationToken);
    }

    private async Task<bool> HasProcessedPostgresAsync(string consumerName, Guid messageId, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(_options.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM inbox_messages WHERE consumer_name = $1 AND message_id = $2";
        command.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Text, consumerName);
        command.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Text, messageId.ToString("D"));
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is not null;
    }

    private async Task MarkProcessedPostgresAsync(string consumerName, Guid messageId, DateTimeOffset processedAt, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(_options.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO inbox_messages (consumer_name, message_id, processed_at_utc)
            VALUES ($1, $2, $3)
            ON CONFLICT (consumer_name, message_id) DO NOTHING;
            """;
        command.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Text, consumerName);
        command.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Text, messageId.ToString("D"));
        command.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.TimestampTz, processedAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<bool> HasProcessedSqliteAsync(string consumerName, Guid messageId, CancellationToken cancellationToken)
    {
        var connection = await GetSqliteConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM inbox_messages WHERE consumer_name = $consumerName AND message_id = $messageId";
        command.Parameters.AddWithValue("$consumerName", consumerName);
        command.Parameters.AddWithValue("$messageId", messageId.ToString("D"));
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is not null;
    }

    private async Task MarkProcessedSqliteAsync(string consumerName, Guid messageId, DateTimeOffset processedAt, CancellationToken cancellationToken)
    {
        var connection = await GetSqliteConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT OR IGNORE INTO inbox_messages (consumer_name, message_id, processed_at_utc)
            VALUES ($consumerName, $messageId, $processedAt);
            """;
        command.Parameters.AddWithValue("$consumerName", consumerName);
        command.Parameters.AddWithValue("$messageId", messageId.ToString("D"));
        command.Parameters.AddWithValue("$processedAt", processedAt.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
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
                    CREATE TABLE IF NOT EXISTS inbox_messages (
                        consumer_name TEXT NOT NULL,
                        message_id TEXT NOT NULL,
                        processed_at_utc TIMESTAMPTZ NOT NULL,
                        PRIMARY KEY (consumer_name, message_id)
                    );
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
                    CREATE TABLE IF NOT EXISTS inbox_messages (
                        consumer_name TEXT NOT NULL,
                        message_id TEXT NOT NULL,
                        processed_at_utc TEXT NOT NULL,
                        PRIMARY KEY (consumer_name, message_id)
                    );
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
