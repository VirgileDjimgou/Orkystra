namespace Orkystra.Contracts.Connectors;

public sealed record ProviderSecretUpdateRequest(string SecretKey, string SecretValue);
