using FleetOps.Core.Common;

namespace FleetOps.Core.Modules.Fleet;

public sealed class DeviceAssignment : TenantEntity
{
    private DeviceAssignment() { }

    public DeviceAssignment(Guid organizationId, Guid deviceId, Guid vehicleId, DateTimeOffset assignedAtUtc)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("Organization is required.", nameof(organizationId));
        }

        if (deviceId == Guid.Empty)
        {
            throw new ArgumentException("Device identifier is required.", nameof(deviceId));
        }

        if (vehicleId == Guid.Empty)
        {
            throw new ArgumentException("Vehicle identifier is required.", nameof(vehicleId));
        }

        OrganizationId = organizationId;
        DeviceId = deviceId;
        VehicleId = vehicleId;
        AssignedAtUtc = assignedAtUtc.ToUniversalTime();
    }

    public Guid DeviceId { get; private init; }
    public Guid VehicleId { get; private init; }
    public DateTimeOffset AssignedAtUtc { get; private init; }
    public DateTimeOffset? UnassignedAtUtc { get; private set; }

    public bool IsActive => UnassignedAtUtc is null;

    public void Close(DateTimeOffset unassignedAtUtc)
    {
        if (UnassignedAtUtc is not null)
        {
            throw new InvalidOperationException("Device assignment is already closed.");
        }

        var universal = unassignedAtUtc.ToUniversalTime();
        if (universal < AssignedAtUtc)
        {
            throw new ArgumentException("Unassignment time cannot precede assignment time.", nameof(unassignedAtUtc));
        }

        UnassignedAtUtc = universal;
    }
}
