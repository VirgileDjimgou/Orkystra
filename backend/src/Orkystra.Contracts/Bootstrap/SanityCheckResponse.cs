namespace Orkystra.Contracts.Bootstrap;

public sealed record SanityCheckResponse(
    string ApiVersion,
    bool AllHealthy,
    IReadOnlyCollection<SanityComponentStatus> Components,
    DateTimeOffset CheckedAtUtc);
