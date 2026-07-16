using FleetOps.Core.Modules.Dispatch;

namespace FleetOps.Api.DriverApp;

public sealed record DriverMissionSummaryResponse(
    Guid Id,
    string Reference,
    string Title,
    MissionStatus Status,
    DateTimeOffset ScheduledStartUtc,
    DateTimeOffset ScheduledEndUtc,
    string? VehicleRegistrationNumber,
    int StopCount,
    int PendingCommandCount,
    long RowVersion);

public sealed record DriverMissionStopResponse(
    Guid Id,
    int Sequence,
    string Name,
    string Address,
    DateTimeOffset PlannedArrivalUtc);

public sealed record DriverMissionTimelineEventResponse(
    Guid Id,
    MissionTimelineEventType EventType,
    string Description,
    DateTimeOffset OccurredAtUtc);

public sealed record DriverMissionDetailResponse(
    Guid Id,
    string Reference,
    string Title,
    MissionStatus Status,
    DateTimeOffset ScheduledStartUtc,
    DateTimeOffset ScheduledEndUtc,
    string? VehicleRegistrationNumber,
    int SimulatedDelayMinutes,
    long RowVersion,
    IReadOnlyList<DriverMissionStopResponse> Stops,
    IReadOnlyList<DriverMissionTimelineEventResponse> Timeline);

public sealed record SyncMissionCommandRequest(
    string CommandId,
    DriverMissionCommandAction Action,
    long RowVersion,
    DateTimeOffset OccurredAtUtc);

public sealed record SyncMissionCommandResponse(
    DriverMissionDetailResponse Mission,
    bool WasDuplicate);
