namespace Orkystra.Api.Persistence;

public sealed record PersistedProjectionSnapshot(
    string TenantId,
    string ProjectionName,
    string ProjectionKey,
    string Source,
    DateTimeOffset CapturedAtUtc,
    string PayloadJson);
