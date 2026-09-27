using FleetOps.Core.Modules.Demo;
using Xunit;

namespace FleetOps.UnitTests;

public sealed class VirtualDriverAgentTests
{
    [Fact]
    public async Task NormalPolicyCompletesMissionThroughExplicitTransitions()
    {
        var tools = new RecordingTools();
        var sink = new RecordingSink();
        var agent = CreateAgent(1, tools, sink);

        for (var tick = 1; tick <= 6; tick++)
            await agent.StepAsync(DemoScenarioKind.NormalShift, tick, DateTimeOffset.UnixEpoch.AddSeconds(tick), default);

        Assert.Equal(VirtualDriverState.Completed, agent.State);
        Assert.Equal(
            [VirtualDriverAction.SubmitInspection, VirtualDriverAction.StartMission, VirtualDriverAction.ArriveAtStop, VirtualDriverAction.SubmitProof, VirtualDriverAction.CompleteMission],
            tools.Actions);
        Assert.Equal(6, sink.Activities.Count);
        Assert.All(sink.Activities, activity => Assert.DoesNotContain("reasoning", activity.ResultMessage, StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(DemoScenarioKind.LateDelivery, VirtualDriverAction.ReportDelay, VirtualDriverState.Blocked)]
    [InlineData(DemoScenarioKind.VehicleIssue, VirtualDriverAction.ReportVehicleIssue, VirtualDriverState.Blocked)]
    [InlineData(DemoScenarioKind.DriverConnectivityLoss, VirtualDriverAction.ReportOffline, VirtualDriverState.Offline)]
    public async Task ControlledFaultsRecoverDeterministically(DemoScenarioKind scenario, VirtualDriverAction faultAction, VirtualDriverState faultState)
    {
        var tools = new RecordingTools();
        var agent = CreateAgent(2, tools, new RecordingSink());
        await agent.StepAsync(scenario, 1, DateTimeOffset.UnixEpoch, default);
        await agent.StepAsync(scenario, 2, DateTimeOffset.UnixEpoch, default);
        var faultTick = scenario == DemoScenarioKind.DriverConnectivityLoss ? 7 : 5;

        await agent.StepAsync(scenario, faultTick, DateTimeOffset.UnixEpoch, default);
        Assert.Equal(faultState, agent.State);
        Assert.Equal(faultAction, tools.Actions[^1]);

        await agent.StepAsync(scenario, faultTick + 1, DateTimeOffset.UnixEpoch, default);
        Assert.Equal(VirtualDriverState.EnRoute, agent.State);
        Assert.Equal(VirtualDriverAction.Recover, tools.Actions[^1]);
    }

    [Fact]
    public async Task RetryableFailureKeepsStateAndUsesIdempotentRetry()
    {
        var tools = new RecordingTools(failFirst: true);
        var agent = CreateAgent(3, tools, new RecordingSink());

        await agent.StepAsync(DemoScenarioKind.NormalShift, 1, DateTimeOffset.UnixEpoch, default);
        Assert.Equal(VirtualDriverState.Assigned, agent.State);
        Assert.Equal(1, agent.RetryCount);
        await agent.StepAsync(DemoScenarioKind.NormalShift, 2, DateTimeOffset.UnixEpoch, default);
        Assert.Equal(VirtualDriverState.PreTrip, agent.State);
        Assert.Equal(0, agent.RetryCount);
        Assert.Equal(tools.IdempotencyKeys[0], tools.IdempotencyKeys[1]);
    }

    [Fact]
    public async Task MultipleAgentsAdvanceIndependently()
    {
        var tools = new RecordingTools();
        var agents = Enumerable.Range(1, 12).Select(index => (IVirtualDriverAgent)CreateAgent(index, tools, new RecordingSink())).ToList();
        var results = await VirtualDriverAgentCoordinator.StepAsync(agents, DemoScenarioKind.NormalShift, 1, DateTimeOffset.UnixEpoch, default);

        Assert.Equal(12, results.Length);
        Assert.All(agents, agent => Assert.Equal(VirtualDriverState.PreTrip, agent.State));
        Assert.Equal(12, tools.Actions.Count);
    }

    [Fact]
    public async Task InvalidDecisionIsRejectedByTransitionGuard()
    {
        var agent = new VirtualDriverAgent(Identity(4), new InvalidDecisionProvider(), new RecordingTools(), new RecordingSink());
        await Assert.ThrowsAsync<InvalidOperationException>(() => agent.StepAsync(DemoScenarioKind.NormalShift, 1, DateTimeOffset.UnixEpoch, default));
    }

    private static VirtualDriverAgent CreateAgent(int index, IVirtualDriverTools tools, IAgentActivitySink sink) =>
        new(Identity(index), new DeterministicAgentDecisionProvider(), tools, sink);

    private static VirtualDriverIdentity Identity(int index) => new(
        Guid.Parse($"10000000-0000-0000-0000-{index:D12}"),
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
        Guid.Parse($"20000000-0000-0000-0000-{index:D12}"),
        Guid.Parse($"30000000-0000-0000-0000-{index:D12}"),
        Guid.Parse($"40000000-0000-0000-0000-{index:D12}"),
        Guid.Parse($"50000000-0000-0000-0000-{index:D12}"));

    private sealed class RecordingTools(bool failFirst = false) : IVirtualDriverTools
    {
        private int attempts;
        public List<VirtualDriverAction> Actions { get; } = [];
        public List<string> IdempotencyKeys { get; } = [];
        public Task<VirtualDriverToolResult> ExecuteAsync(VirtualDriverToolCommand command, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Actions.Add(command.Action);
            IdempotencyKeys.Add(command.IdempotencyKey);
            attempts++;
            return Task.FromResult(failFirst && attempts == 1
                ? new VirtualDriverToolResult(false, true, "temporary", "Retry later.")
                : new VirtualDriverToolResult(true, false, "accepted", "Typed workflow accepted."));
        }
    }

    private sealed class RecordingSink : IAgentActivitySink
    {
        public List<AgentActivity> Activities { get; } = [];
        public Task RecordAsync(AgentActivity activity, CancellationToken cancellationToken)
        {
            Activities.Add(activity);
            return Task.CompletedTask;
        }
    }

    private sealed class InvalidDecisionProvider : IAgentDecisionProvider
    {
        public AgentDecision Decide(AgentObservation observation) => new("invalid-test", VirtualDriverAction.CompleteMission);
    }
}
