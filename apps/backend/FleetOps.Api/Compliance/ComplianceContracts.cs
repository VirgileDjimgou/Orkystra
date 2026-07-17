using FleetOps.Core.Modules.Compliance;

namespace FleetOps.Api.Compliance;

public sealed record ComplianceDocumentTypeResponse(Guid Id, string Name, ComplianceSubjectType SubjectType, bool IsBlocking, bool RequiresReview, bool IsActive, long RowVersion);
public sealed record SaveComplianceDocumentTypeRequest(string Name, ComplianceSubjectType SubjectType, bool IsBlocking, bool RequiresReview, bool IsActive, long? RowVersion);
public sealed record CompliancePolicyResponse(bool BlocksAssignments, long RowVersion, string Disclaimer);
public sealed record UpdateCompliancePolicyRequest(bool BlocksAssignments, long? RowVersion);
public sealed record CreateComplianceDocumentV2Request(Guid? DocumentTypeId, ComplianceSubjectType? SubjectType, Guid? TargetEntityId, string DocumentType, string DocumentNumber, DateTimeOffset ExpiresAtUtc, string? Notes, Guid? MediaAssetId, Guid? ReplacesDocumentId);
public sealed record ReviewComplianceDocumentRequest(bool Approved, long RowVersion);
public sealed record ComplianceMatrixRowResponse(ComplianceSubjectType SubjectType, Guid SubjectId, string SubjectLabel, string DocumentType, Guid? DocumentId, DateTimeOffset? ExpiresAtUtc, string Status, bool IsBlocking, bool IsRisk);
public sealed record CreateCampaignRequest(string Name, DateTimeOffset OpensAtUtc, DateTimeOffset ClosesAtUtc, string TemplateCode, IReadOnlyList<Guid> VehicleIds);
public sealed record CampaignTaskResponse(Guid Id, Guid VehicleId, string VehicleRegistration, Guid DriverId, string DriverName, string TemplateCode, InspectionCampaignTaskStatus Status, DateTimeOffset? SubmittedAtUtc, string? Notes);
public sealed record CampaignResponse(Guid Id, string Name, DateTimeOffset OpensAtUtc, DateTimeOffset ClosesAtUtc, InspectionCampaignStatus Status, long RowVersion, IReadOnlyList<CampaignTaskResponse> Tasks);
public sealed record SubmitCampaignTaskRequest(string CommandId, DateTimeOffset SubmittedAtUtc, string? Notes);
