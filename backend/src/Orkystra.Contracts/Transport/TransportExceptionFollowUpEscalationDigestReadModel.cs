namespace Orkystra.Contracts.Transport;

public sealed record TransportExceptionFollowUpEscalationDigestReadModel(
    int OverdueCount,
    int AtRiskCount,
    int OwnerlessCount,
    int DueWithin24HoursCount,
    int DueWithin72HoursCount,
    int RetiredCount,
    string Summary);
