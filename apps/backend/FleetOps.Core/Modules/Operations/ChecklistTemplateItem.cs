using FleetOps.Core.Common;

namespace FleetOps.Core.Modules.Operations;

public sealed class ChecklistTemplateItem : TenantEntity
{
    private ChecklistTemplateItem() { }

    public ChecklistTemplateItem(
        Guid organizationId,
        Guid checklistTemplateId,
        int sequence,
        string code,
        string label)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("Organization is required.", nameof(organizationId));
        }

        if (checklistTemplateId == Guid.Empty)
        {
            throw new ArgumentException("Checklist template is required.", nameof(checklistTemplateId));
        }

        if (sequence <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence), "Sequence must be positive.");
        }

        OrganizationId = organizationId;
        ChecklistTemplateId = checklistTemplateId;
        Sequence = sequence;
        Code = RequireNonEmpty(code, nameof(code));
        Label = RequireNonEmpty(label, nameof(label));
    }

    public Guid ChecklistTemplateId { get; private init; }
    public int Sequence { get; private init; }
    public string Code { get; private init; } = string.Empty;
    public string Label { get; private init; } = string.Empty;

    private static string RequireNonEmpty(string value, string parameter) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Value is required.", parameter)
            : value.Trim();
}
