using FleetOps.Core.Modules.Integrations;

namespace FleetOps.Api.Integrations;

public sealed record ApiClientCredentialResponse(
    Guid Id,
    string Name,
    ApiClientCredentialType CredentialType,
    string[] Scopes,
    string KeyId,
    string SecretPreview,
    bool IsActive,
    DateTimeOffset? LastUsedAtUtc,
    DateTimeOffset? RevokedAtUtc,
    long RowVersion);

public sealed record CreatedApiClientCredentialResponse(
    Guid Id,
    string Name,
    ApiClientCredentialType CredentialType,
    string[] Scopes,
    string KeyId,
    string PlainTextSecret,
    string SecretPreview,
    bool IsActive,
    long RowVersion);

public sealed record CreateApiClientCredentialRequest(
    string Name,
    ApiClientCredentialType CredentialType,
    string[] Scopes);

public sealed record WebhookEndpointResponse(
    Guid Id,
    string Name,
    string EventType,
    string TargetUrl,
    bool IsActive,
    bool IsSandbox,
    DateTimeOffset? LastSucceededAtUtc,
    DateTimeOffset? DisabledAtUtc,
    long RowVersion);

public sealed record CreateWebhookEndpointRequest(
    string Name,
    string EventType,
    string? TargetUrl,
    string SigningSecret,
    bool IsSandbox);

public sealed record IntegrationContractResponse(
    string EventType,
    string Description,
    object ExamplePayload);

public sealed record IntegrationOutboxMessageResponse(
    Guid Id,
    Guid WebhookEndpointId,
    string EventType,
    string AggregateType,
    string AggregateId,
    IntegrationOutboxStatus Status,
    int AttemptCount,
    DateTimeOffset OccurredAtUtc,
    DateTimeOffset NextAttemptAtUtc,
    DateTimeOffset? DeliveredAtUtc,
    DateTimeOffset? DeadLetteredAtUtc,
    string? LastError);

public sealed record WebhookDeliveryAttemptResponse(
    Guid Id,
    Guid OutboxMessageId,
    Guid WebhookEndpointId,
    int AttemptNumber,
    int? ResponseStatusCode,
    string? ResponseBody,
    bool IsSuccess,
    DateTimeOffset AttemptedAtUtc);

public sealed record SandboxWebhookReceiptResponse(
    Guid Id,
    Guid WebhookEndpointId,
    string EventType,
    string Signature,
    string PayloadJson,
    DateTimeOffset ReceivedAtUtc);

public sealed record PartnerVehicleExportResponse(
    Guid VehicleId,
    string RegistrationNumber,
    string DisplayName,
    bool IsActive,
    int CurrentOdometerKm);

public sealed record DeviceTelemetryIngestionRequest(
    Guid VehicleId,
    string DeviceId,
    string EventId,
    DateTimeOffset RecordedAtUtc,
    double Latitude,
    double Longitude,
    double SpeedKph,
    double HeadingDegrees);
