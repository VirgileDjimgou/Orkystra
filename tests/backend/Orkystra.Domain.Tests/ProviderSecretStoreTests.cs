using Orkystra.Api.Connectors;

namespace Orkystra.Domain.Tests;

public sealed class ProviderSecretStoreTests
{
  [Fact]
  public void HasSecret_returns_false_when_no_secret_is_configured()
  {
    var directory = Directory.CreateTempSubdirectory("orkystra-secret-store-tests");

    try
    {
      var store = new ProviderSecretStore(Path.Combine(directory.FullName, "appsettings.Secrets.local.json"));

      Assert.False(store.HasSecret("rest-transport-adapter", "apiKey"));
      Assert.Null(store.GetSecret("rest-transport-adapter", "apiKey"));
    }
    finally
    {
      directory.Delete(true);
    }
  }

  [Fact]
  public async Task UpdateSecretAsync_persists_and_retrieves_secret_from_file()
  {
    var directory = Directory.CreateTempSubdirectory("orkystra-secret-store-tests");

    try
    {
      var secretsPath = Path.Combine(directory.FullName, "appsettings.Secrets.local.json");
      var store = new ProviderSecretStore(secretsPath);

      await store.UpdateSecretAsync("rest-transport-adapter", "apiKey", "test-api-key-value");

      Assert.True(store.HasSecret("rest-transport-adapter", "apiKey"));
      Assert.Equal("test-api-key-value", store.GetSecret("rest-transport-adapter", "apiKey"));
      Assert.True(File.Exists(secretsPath));

      var persisted = await File.ReadAllTextAsync(secretsPath);
      Assert.Contains("ProviderSecrets", persisted);
      Assert.Contains("rest-transport-adapter", persisted);
      // The secret value must be in the file (local dev only — file is gitignored)
      Assert.Contains("test-api-key-value", persisted);
    }
    finally
    {
      directory.Delete(true);
    }
  }

  [Fact]
  public async Task UpdateSecretAsync_reloads_correctly_from_file_in_new_store_instance()
  {
    var directory = Directory.CreateTempSubdirectory("orkystra-secret-store-tests");

    try
    {
      var secretsPath = Path.Combine(directory.FullName, "appsettings.Secrets.local.json");
      var storeA = new ProviderSecretStore(secretsPath);
      await storeA.UpdateSecretAsync("rest-transport-adapter", "apiKey", "reloaded-key");

      // New instance should read the persisted file.
      var storeB = new ProviderSecretStore(secretsPath);

      Assert.True(storeB.HasSecret("rest-transport-adapter", "apiKey"));
      Assert.Equal("reloaded-key", storeB.GetSecret("rest-transport-adapter", "apiKey"));
    }
    finally
    {
      directory.Delete(true);
    }
  }

  [Fact]
  public async Task UpdateSecretAsync_rejects_unknown_provider()
  {
    var directory = Directory.CreateTempSubdirectory("orkystra-secret-store-tests");

    try
    {
      var store = new ProviderSecretStore(Path.Combine(directory.FullName, "appsettings.Secrets.local.json"));

      await Assert.ThrowsAsync<KeyNotFoundException>(() =>
          store.UpdateSecretAsync("unknown-provider-xyz", "apiKey", "some-value"));
    }
    finally
    {
      directory.Delete(true);
    }
  }

  [Fact]
  public async Task UpdateSecretAsync_rejects_non_secret_field()
  {
    var directory = Directory.CreateTempSubdirectory("orkystra-secret-store-tests");

    try
    {
      var store = new ProviderSecretStore(Path.Combine(directory.FullName, "appsettings.Secrets.local.json"));

      // "baseUrl" is an editable setting, not a secret field.
      await Assert.ThrowsAsync<ArgumentException>(() =>
          store.UpdateSecretAsync("rest-transport-adapter", "baseUrl", "https://example.com"));
    }
    finally
    {
      directory.Delete(true);
    }
  }
}
