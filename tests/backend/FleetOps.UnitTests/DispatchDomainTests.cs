using FleetOps.Core.Modules.Dispatch;
using Xunit;

namespace FleetOps.UnitTests;

public sealed class MissionTests
{
    private static readonly Guid OrgId = Guid.NewGuid();

    [Fact]
    public void ConstructorStartsAsDraftAndAddsTimelineEntry()
    {
        var mission = new Mission(
            OrgId,
            "MIS-001",
            "Morning route",
            new DateTimeOffset(2026, 7, 16, 8, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 16, 10, 0, 0, TimeSpan.Zero));

        Assert.Equal(MissionStatus.Draft, mission.Status);
        Assert.Single(mission.Timeline);
        Assert.Equal("MIS-001", mission.Reference);
    }

    [Fact]
    public void DraftToCompletedDirectTransitionIsRejected()
    {
        var mission = BuildMission();

        Assert.Throws<InvalidOperationException>(() =>
            mission.TransitionTo(MissionStatus.Completed, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void PlannedAssignedEnRouteArrivedCompletedFlowIsValid()
    {
        var mission = BuildMissionWithStops();
        mission.TransitionTo(MissionStatus.Planned, DateTimeOffset.UtcNow);
        mission.SetAssignment(Guid.NewGuid(), Guid.NewGuid());
        mission.TransitionTo(MissionStatus.Assigned, DateTimeOffset.UtcNow);
        mission.TransitionTo(MissionStatus.EnRoute, DateTimeOffset.UtcNow);
        mission.TransitionTo(MissionStatus.Arrived, DateTimeOffset.UtcNow);
        mission.TransitionTo(MissionStatus.Completed, DateTimeOffset.UtcNow);

        Assert.Equal(MissionStatus.Completed, mission.Status);
        Assert.True(mission.Timeline.Count >= 6);
    }

    [Fact]
    public void PlannedToAssignedRequiresAssignment()
    {
        var mission = BuildMissionWithStops();
        mission.TransitionTo(MissionStatus.Planned, DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() =>
            mission.TransitionTo(MissionStatus.Assigned, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void SimulateDelayMovesMissionToDelayed()
    {
        var mission = BuildMissionWithStops();
        mission.TransitionTo(MissionStatus.Planned, DateTimeOffset.UtcNow);
        mission.SetAssignment(Guid.NewGuid(), Guid.NewGuid());
        mission.TransitionTo(MissionStatus.Assigned, DateTimeOffset.UtcNow);

        mission.SimulateDelay(25, DateTimeOffset.UtcNow);

        Assert.Equal(MissionStatus.Delayed, mission.Status);
        Assert.Equal(25, mission.SimulatedDelayMinutes);
    }

    [Fact]
    public void ReplaceStopsRequiresContiguousSequences()
    {
        var mission = BuildMission();

        Assert.Throws<ArgumentException>(() => mission.ReplaceStops(
            [
                new MissionStop(OrgId, mission.Id, 1, "Depot", "1 Main St", DateTimeOffset.UtcNow),
                new MissionStop(OrgId, mission.Id, 3, "Delivery", "2 Main St", DateTimeOffset.UtcNow.AddHours(1)),
            ]));
    }

    private static Mission BuildMission() => new(
        OrgId,
        "MIS-001",
        "Morning route",
        new DateTimeOffset(2026, 7, 16, 8, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 7, 16, 10, 0, 0, TimeSpan.Zero));

    private static Mission BuildMissionWithStops()
    {
        var mission = BuildMission();
        mission.ReplaceStops(
            [
                new MissionStop(OrgId, mission.Id, 1, "Depot", "1 Main St", DateTimeOffset.UtcNow),
                new MissionStop(OrgId, mission.Id, 2, "Customer", "2 Main St", DateTimeOffset.UtcNow.AddHours(1)),
            ]);
        return mission;
    }
}
