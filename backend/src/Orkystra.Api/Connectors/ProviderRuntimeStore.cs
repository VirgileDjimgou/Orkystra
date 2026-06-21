using System.Text.Json;
using Microsoft.Extensions.Options;
using Orkystra.Contracts.Connectors;

namespace Orkystra.Api.Connectors;

public sealed class ProviderRuntimeStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _localConfigurationPath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, ProviderRuntimeSettings> _providers;

    public ProviderRuntimeStore(IOptions<ProviderRuntimeOptions> options, string localConfigurationPath)
    {
        _localConfigurationPath = localConfigurationPath;
        _providers = options.Value.Providers.ToDictionary(
            provider => provider.ProviderId,
            CloneSettings,
            StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<ProviderRuntimeSettings> ListProviders()
    {
        return _providers.Values
            .OrderBy(provider => provider.ProviderId, StringComparer.OrdinalIgnoreCase)
            .Select(CloneSettings)
            .ToArray();
    }

    public ProviderRuntimeSettings? GetProvider(string providerId)
    {
        return _providers.TryGetValue(providerId, out var provider)
            ? CloneSettings(provider)
            : null;
    }

    public async Task<ProviderRuntimeSettings> UpdateAsync(
        string providerId,
        UpdateProviderConfigurationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);

        if (!ProviderRuntimeMetadata.IsKnownProvider(providerId))
        {
            throw new KeyNotFoundException($"Unknown provider '{providerId}'.");
        }

        if (string.IsNullOrWhiteSpace(request.Environment))
        {
            throw new ArgumentException("Environment is required.", nameof(request));
        }

        var editableFields = ProviderRuntimeMetadata.GetEditableFields(providerId);
        var invalidFields = request.Settings.Keys
            .Except(editableFields, StringComparer.OrdinalIgnoreCase)
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (invalidFields.Length > 0)
        {
            throw new ArgumentException(
                $"Unsupported configuration fields for provider '{providerId}': {string.Join(", ", invalidFields)}.",
                nameof(request));
        }

        var normalizedSettings = editableFields.ToDictionary(
            field => field,
            field => request.Settings.TryGetValue(field, out var value) ? value.Trim() : string.Empty,
            StringComparer.OrdinalIgnoreCase);

        var updated = new ProviderRuntimeSettings
        {
            ProviderId = providerId,
            Enabled = request.Enabled,
            Environment = request.Environment.Trim(),
            Settings = normalizedSettings
        };

        await _gate.WaitAsync(cancellationToken);

        try
        {
            _providers[providerId] = updated;
            await PersistAsync(cancellationToken);
            return CloneSettings(updated);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task PersistAsync(CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_localConfigurationPath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var document = new LocalProviderRuntimeConfigurationDocument
        {
            ProviderRuntime = new ProviderRuntimeOptions
            {
                Providers = ListProviders().ToList()
            }
        };

        await using var stream = File.Create(_localConfigurationPath);
        await JsonSerializer.SerializeAsync(stream, document, SerializerOptions, cancellationToken);
    }

    private static ProviderRuntimeSettings CloneSettings(ProviderRuntimeSettings provider)
    {
        return new ProviderRuntimeSettings
        {
            ProviderId = provider.ProviderId,
            Enabled = provider.Enabled,
            Environment = provider.Environment,
            Settings = new Dictionary<string, string>(provider.Settings, StringComparer.OrdinalIgnoreCase)
        };
    }

    private sealed class LocalProviderRuntimeConfigurationDocument
    {
        public ProviderRuntimeOptions ProviderRuntime { get; init; } = new();
    }
}
