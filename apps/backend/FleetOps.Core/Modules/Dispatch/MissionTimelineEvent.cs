using FleetOps.Core.Common;

namespace FleetOps.Core.Modules.Dispatch;

public sealed class MissionTimelineEvent : TenantEntity
{
    private MissionTimelineEvent() { }

    public MissionTimelineEvent(
        Guid organizationId,
        Guid missionId,
        MissionTimelineEventType eventType,
        string description,
        DateTimeOffset occurredAtUtc)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("Organization is required.", nameof(organizationId));
        }

        if (missionId == Guid.Empty)
        {
            throw new ArgumentException("Mission identifier is required.", nameof(missionId));
        }

        OrganizationId = organizationId;
        MissionId = missionId;
        EventType = eventType;
        Description = RequireNonEmpty(description, nameof(description));
        OccurredAtUtc = occurredAtUtc.ToUniversalTime();
    }

    public Guid MissionId { get; private init; }
    public MissionTimelineEventType EventType { get; private init; }
    public string Description { get; private init; } = string.Empty;
    public DateTimeOffset OccurredAtUtc { get; private init; }

    private static string RequireNonEmpty(string value, string parameter) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Value is required.", parameter)
            : value.Trim();
}
