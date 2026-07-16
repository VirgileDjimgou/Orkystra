using FleetOps.Core.Common;

namespace FleetOps.Core.Modules.Operations;

public sealed class PreDepartureInspection : TenantEntity
{
    private readonly List<InspectionItemResult> _items = [];

    private PreDepartureInspection() { }

    public PreDepartureInspection(
        Guid organizationId,
        Guid missionId,
        Guid driverId,
        Guid checklistTemplateId,
        DateTimeOffset completedAtUtc,
        string? notes,
        IEnumerable<InspectionItemResult> items)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("Organization is required.", nameof(organizationId));
        }

        if (missionId == Guid.Empty)
        {
            throw new ArgumentException("Mission is required.", nameof(missionId));
        }

        if (driverId == Guid.Empty)
        {
            throw new ArgumentException("Driver is required.", nameof(driverId));
        }

        if (checklistTemplateId == Guid.Empty)
        {
            throw new ArgumentException("Checklist template is required.", nameof(checklistTemplateId));
        }

        var normalizedItems = items?.OrderBy(x => x.Sequence).ToList()
            ?? throw new ArgumentNullException(nameof(items));
        if (normalizedItems.Count == 0)
        {
            throw new ArgumentException("Inspection must contain at least one checklist result.", nameof(items));
        }

        OrganizationId = organizationId;
        MissionId = missionId;
        DriverId = driverId;
        ChecklistTemplateId = checklistTemplateId;
        CompletedAtUtc = completedAtUtc.ToUniversalTime();
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        _items.AddRange(normalizedItems);
        Outcome = _items.All(x => x.IsPass) ? InspectionOutcome.Passed : InspectionOutcome.Failed;
    }

    public Guid MissionId { get; private init; }
    public Guid DriverId { get; private init; }
    public Guid ChecklistTemplateId { get; private init; }
    public InspectionOutcome Outcome { get; private init; }
    public DateTimeOffset CompletedAtUtc { get; private init; }
    public string? Notes { get; private init; }
    public IReadOnlyCollection<InspectionItemResult> Items => _items;

    public bool HasBlockingCriticalDefect =>
        _items.Any(x => !x.IsPass && x.DefectSeverity == DefectSeverity.Critical);
}
