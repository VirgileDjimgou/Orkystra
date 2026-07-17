using FleetOps.Core.Modules.Dispatch;
using FleetOps.Core.Modules.Operations;

namespace FleetOps.Api.Dispatch;

public sealed record MissionStopRequest(
    int Sequence,
    string Name,
    string Address,
    DateTimeOffset PlannedArrivalUtc);

public sealed record CreateMissionRequest(
    string Reference,
    string Title,
    DateTimeOffset ScheduledStartUtc,
    DateTimeOffset ScheduledEndUtc,
    IReadOnlyList<MissionStopRequest> Stops);

public sealed record UpdateMissionRequest(
    string Title,
    DateTimeOffset ScheduledStartUtc,
    DateTimeOffset ScheduledEndUtc,
    IReadOnlyList<MissionStopRequest> Stops,
    long RowVersion);

public sealed record SetMissionAssignmentRequest(
    Guid DriverId,
    Guid VehicleId,
    long RowVersion,
    string? ComplianceOverrideReason = null);

public sealed record TransitionMissionStatusRequest(
    MissionStatus TargetStatus,
    long RowVersion);

public sealed record SimulateMissionDelayRequest(
    int DelayMinutes,
    long RowVersion);

public sealed record MissionStopResponse(
    Guid Id,
    int Sequence,
    string Name,
    string Address,
    DateTimeOffset PlannedArrivalUtc);

public sealed record MissionTimelineEventResponse(
    Guid Id,
    MissionTimelineEventType EventType,
    string Description,
    DateTimeOffset OccurredAtUtc);

public sealed record MissionInspectionItemResponse(
    int Sequence,
    string Code,
    string Label,
    bool IsPass,
    DefectSeverity DefectSeverity,
    string? Notes,
    string? PhotoReadUrl);

public sealed record MissionInspectionResponse(
    Guid InspectionId,
    InspectionOutcome Outcome,
    bool HasBlockingCriticalDefect,
    DateTimeOffset CompletedAtUtc,
    string? Notes,
    IReadOnlyList<MissionInspectionItemResponse> Items);

public sealed record MissionProofPhotoResponse(
    Guid MediaAssetId,
    string? Caption,
    string PhotoReadUrl);

public sealed record MissionStopProofResponse(
    Guid ProofId,
    Guid MissionStopId,
    string RecipientName,
    string SignatureName,
    DateTimeOffset DeliveredAtUtc,
    string? Notes,
    IReadOnlyList<MissionProofPhotoResponse> Photos);

public sealed record MissionSummaryResponse(
    Guid Id,
    string Reference,
    string Title,
    MissionStatus Status,
    DateTimeOffset ScheduledStartUtc,
    DateTimeOffset ScheduledEndUtc,
    Guid? DriverId,
    string? DriverName,
    Guid? VehicleId,
    string? VehicleRegistrationNumber,
    int StopCount,
    int SimulatedDelayMinutes,
    long RowVersion,
    double? CurrentLatitude,
    double? CurrentLongitude);

public sealed record MissionDetailResponse(
    Guid Id,
    string Reference,
    string Title,
    MissionStatus Status,
    DateTimeOffset ScheduledStartUtc,
    DateTimeOffset ScheduledEndUtc,
    Guid? DriverId,
    string? DriverName,
    Guid? VehicleId,
    string? VehicleRegistrationNumber,
    int SimulatedDelayMinutes,
    long RowVersion,
    MissionInspectionResponse? LatestInspection,
    IReadOnlyList<MissionStopProofResponse> DeliveryProofs,
    IReadOnlyList<MissionStopResponse> Stops,
    IReadOnlyList<MissionTimelineEventResponse> Timeline);
