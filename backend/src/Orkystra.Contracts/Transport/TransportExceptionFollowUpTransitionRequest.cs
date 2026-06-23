namespace Orkystra.Contracts.Transport;

public sealed record TransportExceptionFollowUpTransitionRequest(
    string Action,
    string? Note);
