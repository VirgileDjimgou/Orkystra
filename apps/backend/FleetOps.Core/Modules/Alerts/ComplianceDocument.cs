using FleetOps.Core.Common;
using FleetOps.Core.Modules.Compliance;

namespace FleetOps.Core.Modules.Alerts;

public sealed class ComplianceDocument : TenantEntity
{
    private ComplianceDocument() { }

    public ComplianceDocument(
        Guid organizationId,
        ComplianceDocumentTargetType targetType,
        Guid targetEntityId,
        string documentType,
        string documentNumber,
        DateTimeOffset expiresAtUtc,
        string? notes = null)
    {
        if (organizationId == Guid.Empty) throw new ArgumentException("Organization is required.", nameof(organizationId));
        if (targetEntityId == Guid.Empty) throw new ArgumentException("Target entity is required.", nameof(targetEntityId));

        OrganizationId = organizationId;
        TargetType = targetType;
        TargetEntityId = targetEntityId;
        DocumentType = RequireText(documentType, nameof(documentType), 64);
        DocumentNumber = RequireText(documentNumber, nameof(documentNumber), 64);
        ExpiresAtUtc = expiresAtUtc.ToUniversalTime();
        Notes = NormalizeOptional(notes, 280);
    }

    public ComplianceDocumentTargetType TargetType { get; private set; }
    public Guid TargetEntityId { get; private set; }
    public string DocumentType { get; private set; } = string.Empty;
    public string DocumentNumber { get; private set; } = string.Empty;
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public string? Notes { get; private set; }
    public long RowVersion { get; private set; }
    public Guid? ComplianceDocumentTypeId { get; private set; }
    public Guid? MediaAssetId { get; private set; }
    public ComplianceReviewStatus ReviewStatus { get; private set; } = ComplianceReviewStatus.Approved;
    public Guid? ReplacedByDocumentId { get; private set; }
    public DateTimeOffset? ReviewedAtUtc { get; private set; }
    public Guid? ReviewedByUserId { get; private set; }

    public void UpdateExpiry(DateTimeOffset expiresAtUtc, string? notes)
    {
        ExpiresAtUtc = expiresAtUtc.ToUniversalTime();
        Notes = NormalizeOptional(notes, 280);
        RowVersion++;
    }

    public void Configure(Guid? documentTypeId, Guid? mediaAssetId, bool requiresReview)
    {
        ComplianceDocumentTypeId = documentTypeId; MediaAssetId = mediaAssetId; ReviewStatus = requiresReview ? ComplianceReviewStatus.Pending : ComplianceReviewStatus.Approved; RowVersion++;
    }
    public void Review(Guid reviewerId, bool approved, DateTimeOffset reviewedAtUtc)
    {
        if (reviewerId == Guid.Empty || ReviewStatus != ComplianceReviewStatus.Pending) throw new InvalidOperationException("Document is not awaiting review.");
        ReviewedByUserId = reviewerId; ReviewedAtUtc = reviewedAtUtc.ToUniversalTime(); ReviewStatus = approved ? ComplianceReviewStatus.Approved : ComplianceReviewStatus.Rejected; RowVersion++;
    }
    public void Replace(Guid replacementDocumentId)
    {
        if (replacementDocumentId == Guid.Empty) throw new ArgumentException("Replacement is required.", nameof(replacementDocumentId));
        ReplacedByDocumentId = replacementDocumentId; ReviewStatus = ComplianceReviewStatus.Replaced; RowVersion++;
    }

    private static string RequireText(string value, string parameterName, int maxLength)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            throw new ArgumentException("Value is required.", parameterName);
        }

        return trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
    }
}
