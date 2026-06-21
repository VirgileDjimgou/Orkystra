namespace Orkystra.Contracts.ControlTower;

public sealed record EventFeedItemReadModel(
    string EventId,
    string TimeLabel,
    string Title,
    string Description);
