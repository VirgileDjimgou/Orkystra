namespace Orkystra.Contracts.Transport;

public sealed record TransportExceptionResolutionLedgerReadModel(
    DateTimeOffset UpdatedAtUtc,
    int ResolutionCount,
    IReadOnlyCollection<TransportExceptionResolutionEntryReadModel> Entries);
