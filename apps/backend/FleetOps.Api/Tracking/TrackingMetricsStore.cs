using System.Collections.Concurrent;

namespace FleetOps.Api.Tracking;

public sealed class TrackingMetricsStore
{
    private readonly ConcurrentDictionary<Guid, TrackingMetricsCounter> _counters = new();

    public void RecordAccepted(Guid organizationId, bool outOfOrder)
    {
        var counter = _counters.GetOrAdd(organizationId, _ => new TrackingMetricsCounter());
        counter.Accepted++;
        if (outOfOrder)
        {
            counter.OutOfOrder++;
        }
    }

    public void RecordDuplicate(Guid organizationId)
    {
        var counter = _counters.GetOrAdd(organizationId, _ => new TrackingMetricsCounter());
        counter.Duplicate++;
    }

    public (long Accepted, long Duplicate, long OutOfOrder) GetSnapshot(Guid organizationId)
    {
        if (!_counters.TryGetValue(organizationId, out var counter))
        {
            return (0, 0, 0);
        }

        return (counter.Accepted, counter.Duplicate, counter.OutOfOrder);
    }

    public void Reset(Guid organizationId)
    {
        _counters.TryRemove(organizationId, out _);
    }

    public void ResetAll()
    {
        _counters.Clear();
    }

    private sealed class TrackingMetricsCounter
    {
        public long Accepted;
        public long Duplicate;
        public long OutOfOrder;
    }
}
