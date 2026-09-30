using System.Diagnostics.Metrics;

namespace FleetOps.Core.Observability;

public static class FleetOpsMetrics
{
    public const string MeterName = "FleetOps";

    public static readonly Meter Meter = new(MeterName, "1.0.0");

    public static readonly Histogram<double> TrackingIngestDuration = Meter.CreateHistogram<double>(
        "fleetops.tracking.ingest.duration",
        unit: "ms",
        description: "Duration of a canonical telemetry ingestion request.");

    public static readonly Counter<long> TrackingIngestEvents = Meter.CreateCounter<long>(
        "fleetops.tracking.ingest.events",
        unit: "{event}",
        description: "Telemetry ingestion outcomes (accepted, duplicate, out_of_order, error).");

    public static readonly Histogram<double> TrackingBroadcastDuration = Meter.CreateHistogram<double>(
        "fleetops.tracking.broadcast.duration",
        unit: "ms",
        description: "Duration of a SignalR current-position broadcast.");

    public static readonly Counter<long> TrackingBroadcastEvents = Meter.CreateCounter<long>(
        "fleetops.tracking.broadcast.events",
        unit: "{broadcast}",
        description: "SignalR current-position broadcast outcomes (sent, failed).");

    public static readonly Histogram<double> TrackingSnapshotDuration = Meter.CreateHistogram<double>(
        "fleetops.tracking.snapshot.duration",
        unit: "ms",
        description: "Duration of a tenant snapshot or history read used for catch-up.");

    public static readonly Histogram<double> TrackingResetDuration = Meter.CreateHistogram<double>(
        "fleetops.tracking.reset.duration",
        unit: "ms",
        description: "Duration of a bounded synthetic-scenario reset.");

    public static readonly Counter<long> TrackingResetEvents = Meter.CreateCounter<long>(
        "fleetops.tracking.reset.events",
        unit: "{reset}",
        description: "Synthetic-scenario resets by outcome (completed, conflict).");

    public static readonly Histogram<double> DemoEngineTickDuration = Meter.CreateHistogram<double>(
        "fleetops.demo.engine.tick.duration",
        unit: "ms",
        description: "Duration of one hosted Demo engine tick.");

    public static readonly Counter<long> DemoEngineTelemetryEmitted = Meter.CreateCounter<long>(
        "fleetops.demo.engine.telemetry.emitted",
        unit: "{event}",
        description: "Telemetry events emitted by the hosted Demo engine.");

    public static readonly Histogram<double> DemoEngineStateSaveDuration = Meter.CreateHistogram<double>(
        "fleetops.demo.engine.state.save.duration",
        unit: "ms",
        description: "Duration of one Demo engine snapshot persist.");

    public static readonly Histogram<double> DemoAgentStepDuration = Meter.CreateHistogram<double>(
        "fleetops.demo.agent.step.duration",
        unit: "ms",
        description: "Duration of one virtual-driver agent step.");

    public static readonly Counter<long> DemoAgentSteps = Meter.CreateCounter<long>(
        "fleetops.demo.agent.steps",
        unit: "{step}",
        description: "Virtual-driver agent steps by result code.");

    public static readonly Counter<long> PublicDemoSessions = Meter.CreateCounter<long>(
        "fleetops.public_demo.sessions",
        unit: "{session}",
        description: "Public Demo launch outcomes (launched, disabled, capacity, unavailable).");

    public static readonly Counter<long> PublicDemoControls = Meter.CreateCounter<long>(
        "fleetops.public_demo.controls",
        unit: "{control}",
        description: "Public Demo session controls by action (START, PAUSE, RESET).");
}
