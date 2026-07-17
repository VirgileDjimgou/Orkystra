namespace FleetOps.Api.Onboarding;

public sealed record OnboardingStatusResponse(
    int Vehicles,
    int Drivers,
    int Devices,
    int Operators,
    int DriverAccounts,
    int PairedDriverSessions,
    int ActiveDeviceAssignments,
    int ComplianceDocuments,
    int Missions,
    int CompletedMissions,
    bool AdminMfaEnabled,
    bool HasSampleData,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? FirstValueAtUtc);
public sealed record CreateInvitationRequest(string Email, string FullName, string Role, Guid? DriverId = null);
public sealed record InvitationResponse(Guid InvitationId, string Token, DateTimeOffset ExpiresAtUtc);
public sealed record AcceptInvitationRequest(string Token, string Password);
public sealed record CreatePairingCodeRequest(Guid UserId);
public sealed record PairingCodeResponse(string Code, DateTimeOffset ExpiresAtUtc);
public sealed record ConsumePairingCodeRequest(string Code);
public sealed record ImportPreviewRequest(string TargetType, string Csv);
public sealed record ImportRowError(int Line, string Field, string Message);
public sealed record ImportPreviewResponse(
    Guid PreviewId,
    string TargetType,
    int RowCount,
    IReadOnlyList<ImportRowError> Errors,
    DateTimeOffset ExpiresAtUtc,
    bool CanConfirm,
    long RowVersion);
public sealed record ConfirmImportRequest(long RowVersion = 0);
public sealed record ConfirmImportResponse(int Created, int Updated, int Skipped, bool WasAlreadyConfirmed);
public sealed record ActivationEventRequest(string EventName, string Step);
public sealed record ActivationMetricsResponse(
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? FirstValueAtUtc,
    double? MinutesToFirstValue,
    int AbandonmentCount,
    int ImportErrorCount,
    int EventCount);
public sealed record SampleDataResponse(Guid DataSetId, Guid VehicleId, Guid DriverId, Guid DeviceId, Guid MissionId);
