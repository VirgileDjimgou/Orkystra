namespace Orkystra.Contracts.Bootstrap;

public sealed record SanityComponentStatus(
    string Component,
    bool Healthy,
    string Message,
    long DurationMs);
