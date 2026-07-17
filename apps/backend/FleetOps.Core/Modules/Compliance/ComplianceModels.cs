using FleetOps.Core.Common;

namespace FleetOps.Core.Modules.Compliance;

public enum ComplianceSubjectType { Vehicle, Driver }
public enum ComplianceReviewStatus { Pending, Approved, Rejected, Replaced }
public enum InspectionCampaignStatus { Draft, Active, Closed }
public enum InspectionCampaignTaskStatus { Pending, Submitted }

public sealed class ComplianceDocumentType : TenantEntity
{
    private ComplianceDocumentType() { }
    public ComplianceDocumentType(Guid organizationId, string name, ComplianceSubjectType subjectType, bool isBlocking, bool requiresReview)
    {
        if (organizationId == Guid.Empty) throw new ArgumentException("Organization is required.", nameof(organizationId));
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 80) throw new ArgumentException("A document type name of up to 80 characters is required.", nameof(name));
        OrganizationId = organizationId; Name = name.Trim(); SubjectType = subjectType; IsBlocking = isBlocking; RequiresReview = requiresReview;
    }
    public string Name { get; private set; } = string.Empty;
    public ComplianceSubjectType SubjectType { get; private set; }
    public bool IsBlocking { get; private set; }
    public bool RequiresReview { get; private set; }
    public bool IsActive { get; private set; } = true;
    public long RowVersion { get; private set; }
    public void Update(string name, bool isBlocking, bool requiresReview, bool isActive)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 80) throw new ArgumentException("A document type name of up to 80 characters is required.", nameof(name));
        Name = name.Trim(); IsBlocking = isBlocking; RequiresReview = requiresReview; IsActive = isActive; RowVersion++;
    }
}

public sealed class CompliancePolicy : TenantEntity
{
    private CompliancePolicy() { }
    public CompliancePolicy(Guid organizationId, bool blocksAssignments) { OrganizationId = organizationId; BlocksAssignments = blocksAssignments; }
    public bool BlocksAssignments { get; private set; }
    public long RowVersion { get; private set; }
    public void SetBlocksAssignments(bool value) { BlocksAssignments = value; RowVersion++; }
}

public sealed class ComplianceInspectionCampaign : TenantEntity
{
    private readonly List<ComplianceInspectionCampaignTask> _tasks = [];
    private ComplianceInspectionCampaign() { }
    public ComplianceInspectionCampaign(Guid organizationId, string name, DateTimeOffset opensAtUtc, DateTimeOffset closesAtUtc)
    {
        if (organizationId == Guid.Empty || closesAtUtc <= opensAtUtc) throw new ArgumentException("A valid organization and campaign window are required.");
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 120) throw new ArgumentException("A campaign name of up to 120 characters is required.", nameof(name));
        OrganizationId = organizationId; Name = name.Trim(); OpensAtUtc = opensAtUtc.ToUniversalTime(); ClosesAtUtc = closesAtUtc.ToUniversalTime();
    }
    public string Name { get; private set; } = string.Empty;
    public DateTimeOffset OpensAtUtc { get; private set; }
    public DateTimeOffset ClosesAtUtc { get; private set; }
    public InspectionCampaignStatus Status { get; private set; } = InspectionCampaignStatus.Draft;
    public IReadOnlyCollection<ComplianceInspectionCampaignTask> Tasks => _tasks;
    public long RowVersion { get; private set; }
    public void AddTask(ComplianceInspectionCampaignTask task) { if (task.OrganizationId != OrganizationId) throw new InvalidOperationException("Campaign task tenant mismatch."); _tasks.Add(task); }
    public void Activate() { if (Status != InspectionCampaignStatus.Draft) throw new InvalidOperationException("Only a draft campaign can be activated."); Status = InspectionCampaignStatus.Active; RowVersion++; }
    public void Close() { Status = InspectionCampaignStatus.Closed; RowVersion++; }
}

public sealed class ComplianceInspectionCampaignTask : TenantEntity
{
    private ComplianceInspectionCampaignTask() { }
    public ComplianceInspectionCampaignTask(Guid organizationId, Guid campaignId, Guid vehicleId, Guid driverId, string templateCode)
    {
        if (organizationId == Guid.Empty || campaignId == Guid.Empty || vehicleId == Guid.Empty || driverId == Guid.Empty) throw new ArgumentException("Campaign task identity is required.");
        if (string.IsNullOrWhiteSpace(templateCode) || templateCode.Trim().Length > 64) throw new ArgumentException("Template code is required.", nameof(templateCode));
        OrganizationId = organizationId; CampaignId = campaignId; VehicleId = vehicleId; DriverId = driverId; TemplateCode = templateCode.Trim();
    }
    public Guid CampaignId { get; private init; }
    public Guid VehicleId { get; private init; }
    public Guid DriverId { get; private init; }
    public string TemplateCode { get; private init; } = string.Empty;
    public InspectionCampaignTaskStatus Status { get; private set; }
    public DateTimeOffset? SubmittedAtUtc { get; private set; }
    public string? Notes { get; private set; }
    public string? SubmissionCommandId { get; private set; }
    public void Submit(string commandId, DateTimeOffset submittedAtUtc, string? notes)
    {
        if (Status == InspectionCampaignTaskStatus.Submitted) return;
        if (string.IsNullOrWhiteSpace(commandId)) throw new ArgumentException("Command identifier is required.", nameof(commandId));
        Status = InspectionCampaignTaskStatus.Submitted; SubmissionCommandId = commandId.Trim(); SubmittedAtUtc = submittedAtUtc.ToUniversalTime(); Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
    }
}
