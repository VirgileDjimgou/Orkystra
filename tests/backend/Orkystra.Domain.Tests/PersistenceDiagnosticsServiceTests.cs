using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Orkystra.Api.Observability;
using Orkystra.Api.Persistence;

namespace Orkystra.Domain.Tests;

public sealed class PersistenceDiagnosticsServiceTests
{
    [Fact]
    public async Task BuildAsync_reports_sqlite_path_and_local_posture()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), "orkystra-persistence-diagnostics", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(rootPath);

        var options = Options.Create(new OperationalPersistenceOptions
        {
            Provider = "sqlite",
            DatabasePath = Path.Combine("output", "persistence", "diagnostics.db")
        });
        var store = new SqliteOperationalPersistenceStore(options, rootPath);
        var environment = new StubWebHostEnvironment(rootPath);
        var service = new PersistenceDiagnosticsService(options, store, environment);

        var snapshot = await service.BuildAsync();

        Assert.True(snapshot.Healthy);
        Assert.Equal("sqlite", snapshot.Provider);
        Assert.Equal("local-default", snapshot.Posture);
        Assert.EndsWith(Path.Combine("output", "persistence", "diagnostics.db"), snapshot.ConnectionTarget, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("file-backed-store", snapshot.Signals);
    }

    [Fact]
    public async Task BuildAsync_reports_postgres_target_without_secret_material()
    {
        var options = Options.Create(new OperationalPersistenceOptions
        {
            Provider = "postgres",
            ConnectionString = "Host=localhost;Port=5432;Database=orkystra;Username=orkystra;Password=super-secret"
        });
        var store = new FailingPersistenceStore("connection refused");
        var environment = new StubWebHostEnvironment(Path.GetTempPath());
        var service = new PersistenceDiagnosticsService(options, store, environment);

        var snapshot = await service.BuildAsync();

        Assert.False(snapshot.Healthy);
        Assert.Equal("postgres", snapshot.Provider);
        Assert.Equal("self-host-recommended", snapshot.Posture);
        Assert.Equal("Host=localhost;Port=5432;Database=orkystra;Username=orkystra", snapshot.ConnectionTarget);
        Assert.DoesNotContain("super-secret", snapshot.ConnectionTarget, StringComparison.Ordinal);
        Assert.Contains("connectivity-failed", snapshot.Signals);
    }

    private sealed class StubWebHostEnvironment : IWebHostEnvironment
    {
        public StubWebHostEnvironment(string contentRootPath)
        {
            ContentRootPath = contentRootPath;
        }

        public string ApplicationName { get; set; } = "Orkystra.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = null!;
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; }
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private sealed class FailingPersistenceStore : IOperationalPersistenceStore
    {
        private readonly string _message;

        public FailingPersistenceStore(string message)
        {
            _message = message;
        }

        public Task UpsertProjectionAsync<TPayload>(string tenantId, string projectionName, string projectionKey, string source, TPayload payload, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task AppendWorkflowRunAsync<TPayload>(string tenantId, string workflowKind, string subjectKey, string? scenarioId, string source, string status, TPayload payload, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyCollection<PersistedProjectionSnapshot>> ReadProjectionSnapshotsAsync(string tenantId, string? projectionName, int count, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException(_message);

        public Task<PersistedProjectionSnapshot?> ReadProjectionSnapshotAsync(string tenantId, string projectionName, string projectionKey, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyCollection<PersistedWorkflowRun>> ReadWorkflowRunsAsync(string tenantId, string? workflowKind, int count, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<PersistedWorkflowRun?> ReadWorkflowRunByIdAsync(string tenantId, long runId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
