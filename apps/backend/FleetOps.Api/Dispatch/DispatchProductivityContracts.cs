using FleetOps.Core.Modules.Dispatch;

namespace FleetOps.Api.Dispatch;

public sealed record MissionTemplateStopRequest(int Sequence, string Name, string Address, int ArrivalOffsetMinutes);
public sealed record CreateMissionTemplateRequest(string Name, string Title, IReadOnlyList<MissionTemplateStopRequest> Stops);
public sealed record MissionTemplateResponse(Guid Id, string Name, string Title, long RowVersion, IReadOnlyList<MissionTemplateStopRequest> Stops);
public sealed record DuplicateTemplateRequest(string Reference, DateTimeOffset ScheduledStartUtc, DateTimeOffset ScheduledEndUtc);
public sealed record DispatchImportRow(string Reference, string Title, DateTimeOffset ScheduledStartUtc, DateTimeOffset ScheduledEndUtc, string StopName, string StopAddress, DateTimeOffset PlannedArrivalUtc);
public sealed record DispatchImportPreviewRequest(string ImportKey, IReadOnlyList<DispatchImportRow> Rows);
public sealed record DispatchImportPreviewResponse(string ImportKey, int ValidRows, int InvalidRows, IReadOnlyList<string> Errors, bool AlreadyImported);
public sealed record DispatchBoardResponse(int TotalCount, IReadOnlyList<MissionSummaryResponse> Items);
public sealed record ResourceSuggestionResponse(Guid? DriverId, string? DriverName, Guid? VehicleId, string? VehicleRegistrationNumber, string Explanation);
public sealed record SaveDispatchViewRequest(string Name, string FilterJson);
public sealed record DispatchSavedViewResponse(Guid Id, string Name, string FilterJson);
public sealed record BulkDispatchAssignmentItem(Guid MissionId, Guid DriverId, Guid VehicleId, long RowVersion, string? ComplianceOverrideReason);
public sealed record BulkDispatchAssignmentRequest(IReadOnlyList<BulkDispatchAssignmentItem> Items, bool Confirm);
public sealed record BulkDispatchResult(int Applied, IReadOnlyList<string> Conflicts);
