using System.Collections.Concurrent;

namespace FleetOps.Api.Tracking;

public sealed class TrackingMetricsStore
{
    private readonly ConcurrentDictionary<Guid, TrackingMetricsCounter> _counters = new();

    public void RecordAccepted(Guid organizationId, bool outOfOrder)
    {
        var counter = _counters.GetOrAdd(organizationId, _ => new TrackingMetricsCounter());
        Interlocked.Increment(ref counter.Accepted);
        if (outOfOrder)
        {
            Interlocked.Increment(ref counter.OutOfOrder);
        }
    }

    public void RecordDuplicate(Guid organizationId)
    {
        var counter = _counters.GetOrAdd(organizationId, _ => new TrackingMetricsCounter());
        Interlocked.Increment(ref counter.Duplicate);
    }

    public void RecordRejected(Guid organizationId)
    {
        var counter = _counters.GetOrAdd(organizationId, _ => new TrackingMetricsCounter());
        Interlocked.Increment(ref counter.Rejected);
    }

    public (long Accepted, long Duplicate, long OutOfOrder, long Rejected) GetSnapshot(Guid organizationId)
    {
        if (!_counters.TryGetValue(organizationId, out var counter))
        {
            return (0, 0, 0, 0);
        }

        return (
            Interlocked.Read(ref counter.Accepted),
            Interlocked.Read(ref counter.Duplicate),
            Interlocked.Read(ref counter.OutOfOrder),
            Interlocked.Read(ref counter.Rejected));
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
        public long Rejected;
    }
}
