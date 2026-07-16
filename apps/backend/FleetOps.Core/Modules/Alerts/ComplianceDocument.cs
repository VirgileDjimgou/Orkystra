using FleetOps.Core.Common;

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

    public void UpdateExpiry(DateTimeOffset expiresAtUtc, string? notes)
    {
        ExpiresAtUtc = expiresAtUtc.ToUniversalTime();
        Notes = NormalizeOptional(notes, 280);
        RowVersion++;
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
