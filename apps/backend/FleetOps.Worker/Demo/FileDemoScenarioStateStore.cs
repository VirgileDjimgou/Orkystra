using System.Text.Json;
using FleetOps.Core.Modules.Demo;
using Microsoft.Extensions.Options;

namespace FleetOps.Worker.Demo;

public sealed class FileDemoScenarioStateStore : IDemoScenarioStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly string statePath;

    public FileDemoScenarioStateStore(IOptions<DemoEngineOptions> options, IHostEnvironment environment)
    {
        var configured = options.Value.StatePath;
        if (string.IsNullOrWhiteSpace(configured)) throw new InvalidOperationException("DemoEngine:StatePath is required.");
        statePath = Path.GetFullPath(configured, environment.ContentRootPath);
    }

    public async Task<DemoScenarioSnapshot?> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(statePath)) return null;
        await using var stream = File.OpenRead(statePath);
        return await JsonSerializer.DeserializeAsync<DemoScenarioSnapshot>(stream, JsonOptions, cancellationToken);
    }

    public async Task SaveAsync(DemoScenarioSnapshot snapshot, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(statePath) ?? throw new InvalidOperationException("Demo state path has no directory.");
        Directory.CreateDirectory(directory);
        var temporaryPath = statePath + ".tmp";
        await using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await JsonSerializer.SerializeAsync(stream, snapshot, JsonOptions, cancellationToken);
        }
        File.Move(temporaryPath, statePath, true);
    }

    public Task ClearAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (File.Exists(statePath)) File.Delete(statePath);
        return Task.CompletedTask;
    }
}
