using FleetOps.Core.Modules.Operations;

namespace FleetOps.Api.Operations;

public sealed record ChecklistTemplateItemResponse(
    int Sequence,
    string Code,
    string Label);

public sealed record MediaAssetResponse(
    Guid AssetId,
    string FileName,
    string ContentType,
    long SizeBytes,
    string ReadUrl);

public sealed record InspectionItemResultRequest(
    int Sequence,
    string Code,
    string Label,
    bool IsPass,
    DefectSeverity DefectSeverity,
    string? Notes,
    Guid? PhotoAssetId);

public sealed record InspectionItemResultResponse(
    int Sequence,
    string Code,
    string Label,
    bool IsPass,
    DefectSeverity DefectSeverity,
    string? Notes,
    MediaAssetResponse? Photo);

public sealed record SubmitPreDepartureInspectionRequest(
    string CommandId,
    DateTimeOffset CompletedAtUtc,
    string? Notes,
    IReadOnlyList<InspectionItemResultRequest> Items);

public sealed record PreDepartureInspectionResponse(
    Guid InspectionId,
    InspectionOutcome Outcome,
    bool HasBlockingCriticalDefect,
    DateTimeOffset CompletedAtUtc,
    string? Notes,
    IReadOnlyList<InspectionItemResultResponse> Items);

public sealed record DriverInspectionWorkflowResponse(
    Guid MissionId,
    string MissionReference,
    ChecklistTemplateItemResponse[] ChecklistItems,
    PreDepartureInspectionResponse? LatestInspection);

public sealed record DeliveryProofPhotoRequest(
    Guid MediaAssetId,
    string? Caption);

public sealed record DeliveryProofPhotoResponse(
    Guid MediaAssetId,
    string? Caption,
    MediaAssetResponse Photo);

public sealed record SubmitDeliveryProofRequest(
    string CommandId,
    string RecipientName,
    string SignatureName,
    DateTimeOffset DeliveredAtUtc,
    string? Notes,
    IReadOnlyList<DeliveryProofPhotoRequest> Photos);

public sealed record DeliveryProofResponse(
    Guid ProofId,
    Guid MissionStopId,
    string RecipientName,
    string SignatureName,
    DateTimeOffset DeliveredAtUtc,
    string? Notes,
    IReadOnlyList<DeliveryProofPhotoResponse> Photos);

public sealed record UploadSessionRequest(
    string FileName,
    string ContentType,
    long TotalBytes,
    MediaUploadPurpose Purpose);

public sealed record UploadSessionResponse(
    Guid UploadSessionId,
    long UploadedBytes,
    long TotalBytes,
    DateTimeOffset ExpiresAtUtc,
    bool IsCompleted,
    Guid? MediaAssetId);

public sealed record AppendUploadChunkRequest(
    long Offset,
    string Base64Content);
