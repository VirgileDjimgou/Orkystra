using FleetOps.Core.Common;

namespace FleetOps.Core.Modules.Alerts;

public sealed class VehicleMaintenancePlan : TenantEntity
{
    private VehicleMaintenancePlan() { }

    public VehicleMaintenancePlan(
        Guid organizationId,
        Guid vehicleId,
        string title,
        int? intervalKilometers,
        int? intervalDays,
        int lastCompletedOdometerKm,
        DateTimeOffset lastCompletedAtUtc)
    {
        if (organizationId == Guid.Empty) throw new ArgumentException("Organization is required.", nameof(organizationId));
        if (vehicleId == Guid.Empty) throw new ArgumentException("Vehicle is required.", nameof(vehicleId));
        if (intervalKilometers is null && intervalDays is null)
        {
            throw new ArgumentException("At least one maintenance interval is required.", nameof(intervalKilometers));
        }
        if (intervalKilometers <= 0 && intervalKilometers is not null)
        {
            throw new ArgumentOutOfRangeException(nameof(intervalKilometers));
        }
        if (intervalDays <= 0 && intervalDays is not null)
        {
            throw new ArgumentOutOfRangeException(nameof(intervalDays));
        }
        ArgumentOutOfRangeException.ThrowIfNegative(lastCompletedOdometerKm);

        OrganizationId = organizationId;
        VehicleId = vehicleId;
        Title = RequireText(title, nameof(title), 120);
        IntervalKilometers = intervalKilometers;
        IntervalDays = intervalDays;
        LastCompletedOdometerKm = lastCompletedOdometerKm;
        LastCompletedAtUtc = lastCompletedAtUtc.ToUniversalTime();
    }

    public Guid VehicleId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public int? IntervalKilometers { get; private set; }
    public int? IntervalDays { get; private set; }
    public int LastCompletedOdometerKm { get; private set; }
    public DateTimeOffset LastCompletedAtUtc { get; private set; }
    public bool IsActive { get; private set; } = true;
    public long RowVersion { get; private set; }

    public void MarkCompleted(int completedOdometerKm, DateTimeOffset completedAtUtc)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(completedOdometerKm);
        LastCompletedOdometerKm = completedOdometerKm;
        LastCompletedAtUtc = completedAtUtc.ToUniversalTime();
        RowVersion++;
    }

    public void Deactivate()
    {
        if (!IsActive)
        {
            throw new InvalidOperationException("Maintenance plan is already inactive.");
        }

        IsActive = false;
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
}
