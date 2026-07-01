using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Orkystra.Api.Eventing;
using Orkystra.Api.Observability;
using Orkystra.Contracts.Eventing;

namespace Orkystra.Integration.Tests;

public sealed class OrkystraWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Security:ApiKey"] = "integration-test-key",
                ["OperationalPersistence:Provider"] = "sqlite",
                ["OperationalPersistence:DatabasePath"] = Path.Combine(
                    Path.GetTempPath(),
                    "orkystra-integration-tests",
                    $"persistence-{Guid.NewGuid():N}.db"),
                ["EventBackbone:Enabled"] = "false",
                ["Sanity:FrontendUrl"] = "http://127.0.0.1:9999",
            });
        });

        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IEventBackbonePublisher>(
                _ => new StubEventBackbonePublisher());
            services.AddSingleton<IAuditStore>(
                _ => new StubAuditStore());
        });
    }
}

public sealed class StubEventBackbonePublisher : IEventBackbonePublisher
{
    public List<IEventEnvelope> Published { get; } = [];

    public ValueTask PublishAsync(IEventEnvelope envelope, CancellationToken cancellationToken = default)
    {
        Published.Add(envelope);
        return ValueTask.CompletedTask;
    }
}

public sealed class StubAuditStore : IAuditStore
{
    private readonly List<AuditEntry> _entries = [];

    public ValueTask AppendAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        _entries.Add(entry);
        return ValueTask.CompletedTask;
    }

    public ValueTask<IReadOnlyCollection<AuditEntry>> ReadRecentAsync(int count, CancellationToken cancellationToken = default)
    {
        IReadOnlyCollection<AuditEntry> result = _entries.TakeLast(count).ToList().AsReadOnly();
        return ValueTask.FromResult(result);
    }
}
