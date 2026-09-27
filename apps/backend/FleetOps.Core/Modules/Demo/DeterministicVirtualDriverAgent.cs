namespace FleetOps.Core.Modules.Demo;

public sealed class DeterministicAgentDecisionProvider : IAgentDecisionProvider
{
    public AgentDecision Decide(AgentObservation observation) => observation.State switch
    {
        VirtualDriverState.Assigned => new("assigned-pre-trip", VirtualDriverAction.SubmitInspection),
        VirtualDriverState.PreTrip => new("inspection-passed", VirtualDriverAction.StartMission),
        VirtualDriverState.EnRoute when observation.Scenario == DemoScenarioKind.DriverConnectivityLoss && observation.Tick % 7 == 0 => new("connectivity-fault", VirtualDriverAction.ReportOffline),
        VirtualDriverState.EnRoute when observation.Scenario == DemoScenarioKind.LateDelivery && observation.Tick % 5 == 0 => new("schedule-delay", VirtualDriverAction.ReportDelay),
        VirtualDriverState.EnRoute when observation.Scenario == DemoScenarioKind.VehicleIssue && observation.Tick % 5 == 0 => new("vehicle-diagnostic", VirtualDriverAction.ReportVehicleIssue),
        VirtualDriverState.EnRoute => new("next-stop", VirtualDriverAction.ArriveAtStop),
        VirtualDriverState.AtStop => new("delivery-required", VirtualDriverAction.SubmitProof),
        VirtualDriverState.Delivering => new("proof-accepted", VirtualDriverAction.CompleteMission),
        VirtualDriverState.Completing => new("mission-confirmed", VirtualDriverAction.Finish),
        VirtualDriverState.Offline => new("connectivity-restored", VirtualDriverAction.Recover),
        VirtualDriverState.Blocked => new("exception-cleared", VirtualDriverAction.Recover),
        _ => new("terminal", VirtualDriverAction.Finish)
    };
}

public sealed class VirtualDriverAgent(
    VirtualDriverIdentity identity,
    IAgentDecisionProvider decisions,
    IVirtualDriverTools tools,
    IAgentActivitySink activitySink) : IVirtualDriverAgent
{
    private long sequence;
    private string? pendingIdempotencyKey;
    public VirtualDriverIdentity Identity { get; } = Validate(identity);
    public VirtualDriverState State { get; private set; } = VirtualDriverState.Assigned;
    public int RetryCount { get; private set; }

    public async Task<AgentActivity> StepAsync(
        DemoScenarioKind scenario,
        long tick,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken)
    {
        if (State == VirtualDriverState.Completed)
            return await RecordAsync(new("terminal", VirtualDriverAction.Finish), new(true, false, "already-completed", "Mission is already complete."), occurredAtUtc, cancellationToken);

        var observed = State;
        var decision = decisions.Decide(new(Identity, State, scenario, tick, RetryCount));
        pendingIdempotencyKey ??= $"agent-{Identity.AgentId:N}-{sequence + 1:D12}-{decision.Action}";
        var command = new VirtualDriverToolCommand(Identity, decision.Action, pendingIdempotencyKey, occurredAtUtc);
        var result = decision.Action == VirtualDriverAction.Finish
            ? new VirtualDriverToolResult(true, false, "completed", "Mission workflow completed.")
            : await tools.ExecuteAsync(command, cancellationToken);

        if (result.Succeeded)
        {
            State = NextState(State, decision.Action);
            RetryCount = 0;
            pendingIdempotencyKey = null;
        }
        else if (result.Retryable)
        {
            RetryCount = Math.Min(RetryCount + 1, 3);
        }
        else
        {
            State = VirtualDriverState.Blocked;
            RetryCount = 0;
            pendingIdempotencyKey = null;
        }

        return await RecordAsync(decision, result, occurredAtUtc, cancellationToken, observed);
    }

    private async Task<AgentActivity> RecordAsync(AgentDecision decision, VirtualDriverToolResult result, DateTimeOffset occurredAtUtc, CancellationToken cancellationToken, VirtualDriverState? observed = null)
    {
        var activity = new AgentActivity(Identity, ++sequence, observed ?? State, decision.Policy, decision.Action, result.ResultCode, result.Message, occurredAtUtc);
        await activitySink.RecordAsync(activity, cancellationToken);
        return activity;
    }

    private static VirtualDriverState NextState(VirtualDriverState state, VirtualDriverAction action) => (state, action) switch
    {
        (VirtualDriverState.Assigned, VirtualDriverAction.SubmitInspection) => VirtualDriverState.PreTrip,
        (VirtualDriverState.PreTrip, VirtualDriverAction.StartMission) => VirtualDriverState.EnRoute,
        (VirtualDriverState.EnRoute, VirtualDriverAction.ArriveAtStop) => VirtualDriverState.AtStop,
        (VirtualDriverState.EnRoute, VirtualDriverAction.ReportDelay) => VirtualDriverState.Blocked,
        (VirtualDriverState.EnRoute, VirtualDriverAction.ReportVehicleIssue) => VirtualDriverState.Blocked,
        (VirtualDriverState.EnRoute, VirtualDriverAction.ReportOffline) => VirtualDriverState.Offline,
        (VirtualDriverState.AtStop, VirtualDriverAction.SubmitProof) => VirtualDriverState.Delivering,
        (VirtualDriverState.Delivering, VirtualDriverAction.CompleteMission) => VirtualDriverState.Completing,
        (VirtualDriverState.Completing, VirtualDriverAction.Finish) => VirtualDriverState.Completed,
        (VirtualDriverState.Blocked, VirtualDriverAction.Recover) => VirtualDriverState.EnRoute,
        (VirtualDriverState.Offline, VirtualDriverAction.Recover) => VirtualDriverState.EnRoute,
        _ => throw new InvalidOperationException($"Invalid virtual-driver transition: {state} + {action}.")
    };

    private static VirtualDriverIdentity Validate(VirtualDriverIdentity value)
    {
        if (value.AgentId == Guid.Empty || value.OrganizationId == Guid.Empty || value.DriverId == Guid.Empty || value.VehicleId == Guid.Empty || value.MissionId == Guid.Empty || value.StopId == Guid.Empty)
            throw new ArgumentException("Virtual driver identity must contain all identifiers.", nameof(value));
        return value;
    }
}

public sealed class VirtualDriverAgentCoordinator
{
    public static Task<AgentActivity[]> StepAsync(
        IReadOnlyList<IVirtualDriverAgent> agents,
        DemoScenarioKind scenario,
        long tick,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken) =>
        Task.WhenAll(agents.Select(agent => agent.StepAsync(scenario, tick, occurredAtUtc, cancellationToken)));
}
