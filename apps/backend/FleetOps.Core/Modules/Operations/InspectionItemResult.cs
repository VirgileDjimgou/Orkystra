using FleetOps.Core.Common;

namespace FleetOps.Core.Modules.Operations;

public sealed class InspectionItemResult : TenantEntity
{
    private InspectionItemResult() { }

    public InspectionItemResult(
        Guid organizationId,
        Guid inspectionId,
        int sequence,
        string code,
        string label,
        bool isPass,
        DefectSeverity defectSeverity,
        string? notes,
        Guid? photoAssetId)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("Organization is required.", nameof(organizationId));
        }

        if (inspectionId == Guid.Empty)
        {
            throw new ArgumentException("Inspection is required.", nameof(inspectionId));
        }

        if (sequence <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence), "Sequence must be positive.");
        }

        if (isPass && defectSeverity != DefectSeverity.None)
        {
            throw new InvalidOperationException("A passing checklist item cannot declare a defect severity.");
        }

        if (!isPass && defectSeverity == DefectSeverity.None)
        {
            throw new InvalidOperationException("A failing checklist item must declare a defect severity.");
        }

        OrganizationId = organizationId;
        InspectionId = inspectionId;
        Sequence = sequence;
        Code = RequireNonEmpty(code, nameof(code));
        Label = RequireNonEmpty(label, nameof(label));
        IsPass = isPass;
        DefectSeverity = defectSeverity;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        PhotoAssetId = photoAssetId;
    }

    public Guid InspectionId { get; private init; }
    public int Sequence { get; private init; }
    public string Code { get; private init; } = string.Empty;
    public string Label { get; private init; } = string.Empty;
    public bool IsPass { get; private init; }
    public DefectSeverity DefectSeverity { get; private init; }
    public string? Notes { get; private init; }
    public Guid? PhotoAssetId { get; private init; }

    private static string RequireNonEmpty(string value, string parameter) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Value is required.", parameter)
            : value.Trim();
}
