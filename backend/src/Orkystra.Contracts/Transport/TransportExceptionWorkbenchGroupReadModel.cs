namespace Orkystra.Contracts.Transport;

public sealed record TransportExceptionWorkbenchGroupReadModel(
    string GroupKey,
    string Label,
    string HighestSeverity,
    int Count,
    string Summary,
    string RecommendedAction,
    string ActionLabel);
