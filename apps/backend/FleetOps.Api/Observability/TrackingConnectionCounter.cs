using System.Diagnostics.Metrics;
using FleetOps.Core.Observability;

namespace FleetOps.Api.Observability;

public sealed class TrackingConnectionCounter
{
    private readonly ObservableGauge<long> _gauge;

    public TrackingConnectionCounter()
    {
        _gauge = FleetOpsMetrics.Meter.CreateObservableGauge(
            "fleetops.tracking.signalr.connections",
            () => Interlocked.Read(ref _active),
            unit: "{connection}",
            description: "Active tracking hub connections.");
    }

    private long _active;

    public long Active => Interlocked.Read(ref _active);

    public void OnConnected() => Interlocked.Increment(ref _active);

    public void OnDisconnected() => Interlocked.Decrement(ref _active);
}
