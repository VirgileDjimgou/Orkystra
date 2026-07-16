using FleetOps.Core.Modules.Alerts;

namespace FleetOps.Api.Alerts;

public sealed record AlertSummaryResponse(
    int OpenCount,
    int AcknowledgedCount,
    int CriticalCount,
    int WarningCount,
    int InactiveVehicleCount,
    int MaintenanceCount,
    int ComplianceCount,
    List<AlertListItemResponse> TopAlerts,
    List<AlertNotificationResponse> RecentNotifications);

public sealed record AlertListItemResponse(
    Guid Id,
    AlertRuleType RuleType,
    AlertSeverity Severity,
    AlertStatus Status,
    string Title,
    string Message,
    string TargetType,
    Guid TargetEntityId,
    string TargetLabel,
    Guid? AssignedToUserId,
    string? AssignedToDisplayName,
    Guid? AcknowledgedByUserId,
    string? AcknowledgedByDisplayName,
    DateTimeOffset LastDetectedAtUtc,
    DateTimeOffset? AssignedAtUtc,
    DateTimeOffset? AcknowledgedAtUtc,
    DateTimeOffset? ResolvedAtUtc,
    long RowVersion);

public sealed record AlertNotificationResponse(
    Guid Id,
    Guid AlertId,
    AlertNotificationChannel Channel,
    string Subject,
    string Body,
    DateTimeOffset SentAtUtc);

public sealed record AlertAssigneeResponse(
    Guid UserId,
    string FullName,
    string Email,
    string Role);

public sealed record AssignAlertRequest(Guid AssignedToUserId, long RowVersion);

public sealed record AcknowledgeAlertRequest(long RowVersion);

public sealed record ScanAlertsResponse(
    int CreatedAlerts,
    int RefreshedAlerts,
    int ResolvedAlerts,
    int InAppNotifications,
    int EmailNotifications,
    int EmailFailures);

public sealed record ComplianceDocumentResponse(
    Guid Id,
    Guid TargetEntityId,
    ComplianceDocumentTargetType TargetType,
    string DocumentType,
    string DocumentNumber,
    DateTimeOffset ExpiresAtUtc,
    string? Notes,
    long RowVersion);

public sealed record CreateComplianceDocumentRequest(
    string DocumentType,
    string DocumentNumber,
    DateTimeOffset ExpiresAtUtc,
    string? Notes);

public sealed record UpdateComplianceDocumentRequest(
    DateTimeOffset ExpiresAtUtc,
    string? Notes,
    long RowVersion);

public sealed record VehicleMaintenancePlanResponse(
    Guid Id,
    Guid VehicleId,
    string Title,
    int? IntervalKilometers,
    int? IntervalDays,
    int LastCompletedOdometerKm,
    DateTimeOffset LastCompletedAtUtc,
    bool IsActive,
    long RowVersion);

public sealed record CreateVehicleMaintenancePlanRequest(
    string Title,
    int? IntervalKilometers,
    int? IntervalDays,
    int LastCompletedOdometerKm,
    DateTimeOffset LastCompletedAtUtc);

public sealed record CompleteVehicleMaintenancePlanRequest(
    int CompletedOdometerKm,
    DateTimeOffset CompletedAtUtc,
    long RowVersion);

public sealed record UpdateVehicleOdometerRequest(int CurrentOdometerKm, long RowVersion);
