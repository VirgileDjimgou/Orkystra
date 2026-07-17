namespace FleetOps.Api.Operations;

public sealed record OperationsExceptionQueueResponse(
    OperationsQueueSummaryResponse Summary,
    IReadOnlyList<OperationsExceptionListItemResponse> Items);

public sealed record OperationsQueueSummaryResponse(
    int TotalActive,
    int CriticalCount,
    int WarningCount,
    int SnoozedCount,
    int UnassignedCount);

public sealed record OperationsExceptionListItemResponse(
    string Id,
    string SourceType,
    string Severity,
    string WorkflowStatus,
    string Title,
    string Message,
    DateTimeOffset DetectedAtUtc,
    DateTimeOffset? SnoozedUntilUtc,
    string? SnoozeReason,
    DateTimeOffset? ResolvedAtUtc,
    string? ResolutionReason,
    Guid? AssignedToUserId,
    string? AssignedToDisplayName,
    Guid? AcknowledgedByUserId,
    string? AcknowledgedByDisplayName,
    string SearchText,
    long SourceRowVersion,
    long StateRowVersion,
    string ConcurrencyToken,
    OperationsExceptionLinkResponse Links);

public sealed record OperationsExceptionLinkResponse(
    Guid? MissionId,
    string? MissionReference,
    Guid? VehicleId,
    string? VehicleRegistrationNumber,
    Guid? DriverId,
    string? DriverName,
    Guid? AlertId,
    Guid? InspectionId,
    Guid? SyncIncidentId);

public sealed record OperationsSavedViewResponse(
    Guid Id,
    string Name,
    bool IsShared,
    OperationsSavedViewFilterRequest Filters,
    long RowVersion,
    Guid CreatedByUserId);

public sealed record OperationsSavedViewFilterRequest(
    string? Search,
    string? SourceType,
    string? Severity,
    string? WorkflowStatus,
    Guid? AssignedToUserId,
    bool IncludeSnoozed);

public sealed record CreateOperationsSavedViewRequest(
    string Name,
    bool IsShared,
    OperationsSavedViewFilterRequest Filters);

public sealed record UpdateOperationsSavedViewRequest(
    string Name,
    bool IsShared,
    OperationsSavedViewFilterRequest Filters,
    long RowVersion);

public sealed record OperationsAssignRequest(Guid AssignedToUserId, string ConcurrencyToken);

public sealed record OperationsActionRequest(string ConcurrencyToken);

public sealed record OperationsResolveRequest(string ConcurrencyToken, string Reason);

public sealed record OperationsSnoozeRequest(string ConcurrencyToken, DateTimeOffset SnoozedUntilUtc, string Reason);

public sealed record OperationsBulkActionItemRequest(string Id, string ConcurrencyToken);

public sealed record OperationsBulkActionRequest(
    string Action,
    string? Reason,
    DateTimeOffset? SnoozedUntilUtc,
    Guid? AssignedToUserId,
    IReadOnlyList<OperationsBulkActionItemRequest> Items);

public sealed record OperationsBulkActionResponse(
    int SuccessCount,
    IReadOnlyList<string> FailedIds);
