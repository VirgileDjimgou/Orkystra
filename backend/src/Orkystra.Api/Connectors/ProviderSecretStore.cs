using System.Text.Json;

namespace Orkystra.Api.Connectors;

/// <summary>
/// Stores provider secrets (such as API keys) outside the regular provider runtime settings.
/// Secrets are loaded from environment variables first and fall back to an ignored local secrets file.
/// Secret values are NEVER returned to callers — only presence (<see cref="HasSecret"/>) is exposed.
/// </summary>
public sealed class ProviderSecretStore
{
  private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

  private readonly string _secretsFilePath;
  private readonly SemaphoreSlim _gate = new(1, 1);

  // [providerId][secretKey] = value — only file-backed entries; env vars are checked at call time.
  private readonly Dictionary<string, Dictionary<string, string>> _fileSecrets;

  public ProviderSecretStore(string secretsFilePath)
  {
    _secretsFilePath = secretsFilePath;
    _fileSecrets = LoadFromFile(secretsFilePath);
  }

  /// <summary>Returns true when a non-empty secret value is available for the given provider and key.</summary>
  public bool HasSecret(string providerId, string secretKey)
  {
    if (!string.IsNullOrWhiteSpace(GetFromEnvironment(providerId, secretKey)))
    {
      return true;
    }

    return _fileSecrets.TryGetValue(providerId, out var providerSecrets)
        && providerSecrets.TryGetValue(secretKey, out var value)
        && !string.IsNullOrWhiteSpace(value);
  }

  /// <summary>
  /// Returns the secret value for the given provider and key, or null when not configured.
  /// Environment variables take precedence over the local secrets file.
  /// </summary>
  public string? GetSecret(string providerId, string secretKey)
  {
    var envValue = GetFromEnvironment(providerId, secretKey);
    if (!string.IsNullOrWhiteSpace(envValue))
    {
      return envValue;
    }

    if (_fileSecrets.TryGetValue(providerId, out var providerSecrets)
        && providerSecrets.TryGetValue(secretKey, out var value)
        && !string.IsNullOrWhiteSpace(value))
    {
      return value;
    }

    return null;
  }

  /// <summary>Stores a secret value in the local secrets file for the given provider and key.</summary>
  public async Task UpdateSecretAsync(
      string providerId,
      string secretKey,
      string secretValue,
      CancellationToken cancellationToken = default)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
    ArgumentException.ThrowIfNullOrWhiteSpace(secretKey);

    if (!ProviderRuntimeMetadata.IsKnownProvider(providerId))
    {
      throw new KeyNotFoundException($"Unknown provider '{providerId}'.");
    }

    if (!ProviderRuntimeMetadata.IsSecretField(providerId, secretKey))
    {
      throw new ArgumentException(
          $"'{secretKey}' is not a registered secret field for provider '{providerId}'.",
          nameof(secretKey));
    }

    await _gate.WaitAsync(cancellationToken);

    try
    {
      if (!_fileSecrets.ContainsKey(providerId))
      {
        _fileSecrets[providerId] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
      }

      _fileSecrets[providerId][secretKey] = secretValue.Trim();
      await PersistAsync(cancellationToken);
    }
    finally
    {
      _gate.Release();
    }
  }

  // Resolves env var using convention ORKYSTRA_PROVIDER_{PROVIDER_ID_UPPER}_{SECRET_KEY_UPPER}
  // e.g., rest-transport-adapter + apiKey → ORKYSTRA_PROVIDER_REST_TRANSPORT_ADAPTER_APIKEY
  private static string? GetFromEnvironment(string providerId, string secretKey)
  {
    var normalizedId = providerId.Replace("-", "_").ToUpperInvariant();
    var normalizedKey = secretKey.ToUpperInvariant();
    var envVarName = $"ORKYSTRA_PROVIDER_{normalizedId}_{normalizedKey}";
    return Environment.GetEnvironmentVariable(envVarName);
  }

  private static Dictionary<string, Dictionary<string, string>> LoadFromFile(string path)
  {
    if (!File.Exists(path))
    {
      return [];
    }

    try
    {
      var json = File.ReadAllText(path);
      var doc = JsonSerializer.Deserialize<LocalSecretsDocument>(json, SerializerOptions);
      return doc?.ProviderSecrets ?? [];
    }
    catch
    {
      return [];
    }
  }

  private async Task PersistAsync(CancellationToken cancellationToken)
  {
    var directory = Path.GetDirectoryName(_secretsFilePath);

    if (!string.IsNullOrWhiteSpace(directory))
    {
      Directory.CreateDirectory(directory);
    }

    var doc = new LocalSecretsDocument
    {
      ProviderSecrets = new Dictionary<string, Dictionary<string, string>>(
            _fileSecrets.ToDictionary(
                kvp => kvp.Key,
                kvp => new Dictionary<string, string>(kvp.Value, StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase))
    };

    await using var stream = File.Create(_secretsFilePath);
    await JsonSerializer.SerializeAsync(stream, doc, SerializerOptions, cancellationToken);
  }

  private sealed class LocalSecretsDocument
  {
    public Dictionary<string, Dictionary<string, string>> ProviderSecrets { get; init; }
        = new(StringComparer.OrdinalIgnoreCase);
  }
}
