using Microsoft.Extensions.Options;
using Npgsql;
using Orkystra.Api.Persistence;

namespace Orkystra.Api.Observability;

public sealed class PersistenceDiagnosticsService
{
    private readonly OperationalPersistenceOptions _options;
    private readonly IOperationalPersistenceStore _persistenceStore;
    private readonly string _contentRootPath;

    public PersistenceDiagnosticsService(
        IOptions<OperationalPersistenceOptions> options,
        IOperationalPersistenceStore persistenceStore,
        IWebHostEnvironment environment)
    {
        _options = options.Value;
        _persistenceStore = persistenceStore;
        _contentRootPath = environment.ContentRootPath;
    }

    public async ValueTask<PersistenceDiagnosticsSnapshot> BuildAsync(CancellationToken cancellationToken = default)
    {
        var provider = string.Equals(_options.Provider, "postgres", StringComparison.OrdinalIgnoreCase)
            ? "postgres"
            : "sqlite";

        var posture = provider == "postgres"
            ? "self-host-recommended"
            : "local-default";

        string connectionTarget;
        string[] signals;

        if (provider == "postgres")
        {
            var builder = new NpgsqlConnectionStringBuilder(_options.ConnectionString);
            connectionTarget = $"Host={builder.Host};Port={builder.Port};Database={builder.Database};Username={builder.Username}";
            signals = ["networked-store", "shared-persistence-ready"];
        }
        else
        {
            var resolvedPath = Path.GetFullPath(Path.IsPathRooted(_options.DatabasePath)
                ? _options.DatabasePath
                : Path.Combine(_contentRootPath, _options.DatabasePath));
            connectionTarget = resolvedPath;
            signals = ["file-backed-store", "local-default"];
        }

        try
        {
            await _persistenceStore.ReadProjectionSnapshotsAsync("persistence-diagnostics", null, 1, cancellationToken);

            return new PersistenceDiagnosticsSnapshot(
                Provider: provider,
                Posture: posture,
                ConnectionTarget: connectionTarget,
                Healthy: true,
                Message: provider == "postgres"
                    ? "PostgreSQL operational persistence is reachable and initialized."
                    : "SQLite operational persistence is reachable and initialized.",
                Signals: signals,
                VerifiedAtUtc: DateTimeOffset.UtcNow);
        }
        catch (Exception exception)
        {
            return new PersistenceDiagnosticsSnapshot(
                Provider: provider,
                Posture: posture,
                ConnectionTarget: connectionTarget,
                Healthy: false,
                Message: $"Operational persistence check failed: {exception.Message}",
                Signals: [.. signals, "connectivity-failed"],
                VerifiedAtUtc: DateTimeOffset.UtcNow);
        }
    }
}

public sealed record PersistenceDiagnosticsSnapshot(
    string Provider,
    string Posture,
    string ConnectionTarget,
    bool Healthy,
    string Message,
    IReadOnlyCollection<string> Signals,
    DateTimeOffset VerifiedAtUtc);
