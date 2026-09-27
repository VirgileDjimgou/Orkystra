using FleetOps.Core.Common;

namespace FleetOps.Core.Modules.Demo;

public enum VirtualDriverState
{
    Available,
    Assigned,
    PreTrip,
    EnRoute,
    AtStop,
    Delivering,
    Completing,
    Completed,
    Blocked,
    Offline
}

public enum VirtualDriverAction
{
    SubmitInspection,
    StartMission,
    ArriveAtStop,
    SubmitProof,
    CompleteMission,
    ReportDelay,
    ReportVehicleIssue,
    ReportOffline,
    Recover,
    Finish
}

public sealed record VirtualDriverIdentity(
    Guid AgentId,
    Guid OrganizationId,
    Guid DriverId,
    Guid VehicleId,
    Guid MissionId,
    Guid StopId);

public sealed record AgentObservation(
    VirtualDriverIdentity Identity,
    VirtualDriverState State,
    DemoScenarioKind Scenario,
    long Tick,
    int RetryCount);

public sealed record AgentDecision(string Policy, VirtualDriverAction Action);

public sealed record VirtualDriverToolCommand(
    VirtualDriverIdentity Identity,
    VirtualDriverAction Action,
    string IdempotencyKey,
    DateTimeOffset OccurredAtUtc);

public sealed record VirtualDriverToolResult(bool Succeeded, bool Retryable, string ResultCode, string Message);

public interface IAgentDecisionProvider
{
    AgentDecision Decide(AgentObservation observation);
}

public interface IVirtualDriverTools
{
    Task<VirtualDriverToolResult> ExecuteAsync(VirtualDriverToolCommand command, CancellationToken cancellationToken);
}

public interface IAgentActivitySink
{
    Task RecordAsync(AgentActivity activity, CancellationToken cancellationToken);
}

public interface IVirtualDriverAgent
{
    VirtualDriverIdentity Identity { get; }
    VirtualDriverState State { get; }
    int RetryCount { get; }
    Task<AgentActivity> StepAsync(DemoScenarioKind scenario, long tick, DateTimeOffset occurredAtUtc, CancellationToken cancellationToken);
}

public sealed class AgentActivity : TenantEntity
{
    private AgentActivity() { }

    public AgentActivity(
        VirtualDriverIdentity identity,
        long sequence,
        VirtualDriverState observedState,
        string policy,
        VirtualDriverAction action,
        string resultCode,
        string resultMessage,
        DateTimeOffset occurredAtUtc,
        Guid? id = null)
    {
        if (identity.OrganizationId == Guid.Empty) throw new ArgumentException("Organization is required.", nameof(identity));
        ArgumentOutOfRangeException.ThrowIfLessThan(sequence, 1);
        OrganizationId = identity.OrganizationId;
        AgentId = identity.AgentId;
        DriverId = identity.DriverId;
        VehicleId = identity.VehicleId;
        MissionId = identity.MissionId;
        StopId = identity.StopId;
        Sequence = sequence;
        ObservedState = observedState;
        Policy = Require(policy, nameof(policy), 80);
        Action = action;
        ResultCode = Require(resultCode, nameof(resultCode), 80);
        ResultMessage = Require(resultMessage, nameof(resultMessage), 240);
        OccurredAtUtc = occurredAtUtc.ToUniversalTime();
        if (id.HasValue) Id = id.Value;
    }

    public Guid AgentId { get; private init; }
    public Guid DriverId { get; private init; }
    public Guid VehicleId { get; private init; }
    public Guid MissionId { get; private init; }
    public Guid StopId { get; private init; }
    public long Sequence { get; private init; }
    public VirtualDriverState ObservedState { get; private init; }
    public string Policy { get; private init; } = string.Empty;
    public VirtualDriverAction Action { get; private init; }
    public string ResultCode { get; private init; } = string.Empty;
    public string ResultMessage { get; private init; } = string.Empty;
    public DateTimeOffset OccurredAtUtc { get; private init; }

    private static string Require(string value, string parameter, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Value is required.", parameter);
        var trimmed = value.Trim();
        if (trimmed.Length > maxLength) throw new ArgumentOutOfRangeException(parameter);
        return trimmed;
    }
}
