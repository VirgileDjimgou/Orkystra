using FleetOps.Core.Common;

namespace FleetOps.Core.Modules.Dispatch;

public sealed class Mission : TenantEntity
{
    private readonly List<MissionStop> _stops = [];
    private readonly List<MissionTimelineEvent> _timeline = [];

    private Mission() { }

    public Mission(
        Guid organizationId,
        string reference,
        string title,
        DateTimeOffset scheduledStartUtc,
        DateTimeOffset scheduledEndUtc)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("Organization is required.", nameof(organizationId));
        }

        var startUtc = scheduledStartUtc.ToUniversalTime();
        var endUtc = scheduledEndUtc.ToUniversalTime();
        if (endUtc <= startUtc)
        {
            throw new ArgumentException("Scheduled end must be later than scheduled start.", nameof(scheduledEndUtc));
        }

        OrganizationId = organizationId;
        Reference = RequireNonEmpty(reference, nameof(reference));
        Title = RequireNonEmpty(title, nameof(title));
        ScheduledStartUtc = startUtc;
        ScheduledEndUtc = endUtc;
        Status = MissionStatus.Draft;
        AppendTimeline(MissionTimelineEventType.Created, $"Mission {Reference} created.", DateTimeOffset.UtcNow);
    }

    public string Reference { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public MissionStatus Status { get; private set; }
    public DateTimeOffset ScheduledStartUtc { get; private set; }
    public DateTimeOffset ScheduledEndUtc { get; private set; }
    public Guid? DriverId { get; private set; }
    public Guid? VehicleId { get; private set; }
    public int SimulatedDelayMinutes { get; private set; }
    public long RowVersion { get; private set; }
    public IReadOnlyCollection<MissionStop> Stops => _stops;
    public IReadOnlyCollection<MissionTimelineEvent> Timeline => _timeline;

    public void UpdateDetails(
        string title,
        DateTimeOffset scheduledStartUtc,
        DateTimeOffset scheduledEndUtc)
    {
        EnsureMutablePlan();

        var startUtc = scheduledStartUtc.ToUniversalTime();
        var endUtc = scheduledEndUtc.ToUniversalTime();
        if (endUtc <= startUtc)
        {
            throw new ArgumentException("Scheduled end must be later than scheduled start.", nameof(scheduledEndUtc));
        }

        Title = RequireNonEmpty(title, nameof(title));
        ScheduledStartUtc = startUtc;
        ScheduledEndUtc = endUtc;
        Touch();
        AppendTimeline(MissionTimelineEventType.Updated, "Mission details updated.", DateTimeOffset.UtcNow);
    }

    public void ReplaceStops(IEnumerable<MissionStop> stops)
    {
        EnsureMutablePlan();

        var ordered = stops.OrderBy(x => x.Sequence).ToList();
        if (ordered.Count == 0)
        {
            throw new ArgumentException("At least one stop is required.", nameof(stops));
        }

        for (var index = 0; index < ordered.Count; index++)
        {
            if (ordered[index].Sequence != index + 1)
            {
                throw new ArgumentException("Stops must use a contiguous sequence starting at 1.", nameof(stops));
            }
        }

        _stops.Clear();
        _stops.AddRange(ordered);
        Touch();
        AppendTimeline(MissionTimelineEventType.Updated, "Mission stops updated.", DateTimeOffset.UtcNow);
    }

    public void SetAssignment(Guid driverId, Guid vehicleId)
    {
        if (driverId == Guid.Empty)
        {
            throw new ArgumentException("Driver identifier is required.", nameof(driverId));
        }

        if (vehicleId == Guid.Empty)
        {
            throw new ArgumentException("Vehicle identifier is required.", nameof(vehicleId));
        }

        if (Status is MissionStatus.EnRoute or MissionStatus.Arrived or MissionStatus.Completed or MissionStatus.Cancelled)
        {
            throw new InvalidOperationException("Mission assignment cannot be changed in the current status.");
        }

        DriverId = driverId;
        VehicleId = vehicleId;
        Touch();
        AppendTimeline(MissionTimelineEventType.AssignmentChanged, "Driver and vehicle assigned.", DateTimeOffset.UtcNow);
    }

    public void TransitionTo(MissionStatus nextStatus, DateTimeOffset occurredAtUtc)
    {
        occurredAtUtc = occurredAtUtc.ToUniversalTime();
        if (nextStatus == Status)
        {
            throw new InvalidOperationException("Mission is already in the requested status.");
        }

        switch (Status)
        {
            case MissionStatus.Draft when nextStatus == MissionStatus.Planned:
                EnsureStopsDefined();
                break;
            case MissionStatus.Draft when nextStatus == MissionStatus.Cancelled:
                break;
            case MissionStatus.Planned when nextStatus is MissionStatus.Assigned or MissionStatus.Cancelled:
                if (nextStatus == MissionStatus.Assigned)
                {
                    EnsureAssignmentDefined();
                }
                break;
            case MissionStatus.Assigned when nextStatus is MissionStatus.EnRoute or MissionStatus.Delayed or MissionStatus.Cancelled:
                break;
            case MissionStatus.EnRoute when nextStatus is MissionStatus.Arrived or MissionStatus.Delayed:
                break;
            case MissionStatus.Arrived when nextStatus is MissionStatus.Completed or MissionStatus.Delayed:
                break;
            case MissionStatus.Delayed when nextStatus is MissionStatus.Assigned or MissionStatus.EnRoute or MissionStatus.Cancelled:
                break;
            default:
                throw new InvalidOperationException($"Cannot transition mission from {Status} to {nextStatus}.");
        }

        Status = nextStatus;
        if (nextStatus != MissionStatus.Delayed)
        {
            SimulatedDelayMinutes = 0;
        }

        Touch();
        AppendTimeline(MissionTimelineEventType.StatusChanged, $"Mission status changed to {nextStatus}.", occurredAtUtc);
    }

    public void SimulateDelay(int delayMinutes, DateTimeOffset occurredAtUtc)
    {
        if (delayMinutes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(delayMinutes), "Delay must be positive.");
        }

        if (Status is MissionStatus.Completed or MissionStatus.Cancelled or MissionStatus.Draft)
        {
            throw new InvalidOperationException("Delay cannot be simulated in the current status.");
        }

        SimulatedDelayMinutes = delayMinutes;
        Status = MissionStatus.Delayed;
        Touch();
        AppendTimeline(
            MissionTimelineEventType.DelaySimulated,
            $"Mission delay simulated: {delayMinutes} minutes.",
            occurredAtUtc.ToUniversalTime());
    }

    private void EnsureStopsDefined()
    {
        if (_stops.Count == 0)
        {
            throw new InvalidOperationException("Mission must contain at least one stop.");
        }
    }

    private void EnsureAssignmentDefined()
    {
        if (DriverId is null || VehicleId is null)
        {
            throw new InvalidOperationException("Mission must be assigned before entering Assigned status.");
        }
    }

    private void EnsureMutablePlan()
    {
        if (Status is MissionStatus.Completed or MissionStatus.Cancelled or MissionStatus.EnRoute or MissionStatus.Arrived)
        {
            throw new InvalidOperationException("Mission plan cannot be changed in the current status.");
        }
    }

    private void Touch() => RowVersion++;

    private void AppendTimeline(MissionTimelineEventType eventType, string description, DateTimeOffset occurredAtUtc)
    {
        _timeline.Add(new MissionTimelineEvent(OrganizationId, Id, eventType, description, occurredAtUtc));
    }

    private static string RequireNonEmpty(string value, string parameter) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Value is required.", parameter)
            : value.Trim();
}
