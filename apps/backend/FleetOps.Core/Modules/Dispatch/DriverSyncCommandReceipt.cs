using FleetOps.Core.Common;

namespace FleetOps.Core.Modules.Dispatch;

public sealed class DriverSyncCommandReceipt : TenantEntity
{
    private DriverSyncCommandReceipt() { }

    public DriverSyncCommandReceipt(
        Guid organizationId,
        Guid driverId,
        Guid missionId,
        string commandId,
        DriverMissionCommandAction action,
        DateTimeOffset processedAtUtc)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("Organization is required.", nameof(organizationId));
        }

        if (driverId == Guid.Empty)
        {
            throw new ArgumentException("Driver identifier is required.", nameof(driverId));
        }

        if (missionId == Guid.Empty)
        {
            throw new ArgumentException("Mission identifier is required.", nameof(missionId));
        }

        OrganizationId = organizationId;
        DriverId = driverId;
        MissionId = missionId;
        CommandId = RequireNonEmpty(commandId, nameof(commandId));
        Action = action;
        ProcessedAtUtc = processedAtUtc.ToUniversalTime();
    }

    public Guid DriverId { get; private init; }
    public Guid MissionId { get; private init; }
    public string CommandId { get; private init; } = string.Empty;
    public DriverMissionCommandAction Action { get; private init; }
    public DateTimeOffset ProcessedAtUtc { get; private init; }

    private static string RequireNonEmpty(string value, string parameter) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Value is required.", parameter)
            : value.Trim();
}
