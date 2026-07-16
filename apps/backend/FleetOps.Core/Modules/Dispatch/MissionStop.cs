using FleetOps.Core.Common;

namespace FleetOps.Core.Modules.Dispatch;

public sealed class MissionStop : TenantEntity
{
    private MissionStop() { }

    public MissionStop(
        Guid organizationId,
        Guid missionId,
        int sequence,
        string name,
        string address,
        DateTimeOffset plannedArrivalUtc)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("Organization is required.", nameof(organizationId));
        }

        if (missionId == Guid.Empty)
        {
            throw new ArgumentException("Mission identifier is required.", nameof(missionId));
        }

        if (sequence <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence), "Sequence must be positive.");
        }

        OrganizationId = organizationId;
        MissionId = missionId;
        Sequence = sequence;
        Name = RequireNonEmpty(name, nameof(name));
        Address = RequireNonEmpty(address, nameof(address));
        PlannedArrivalUtc = plannedArrivalUtc.ToUniversalTime();
    }

    public Guid MissionId { get; private init; }
    public int Sequence { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Address { get; private set; } = string.Empty;
    public DateTimeOffset PlannedArrivalUtc { get; private set; }

    private static string RequireNonEmpty(string value, string parameter) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Value is required.", parameter)
            : value.Trim();
}
