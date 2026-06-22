namespace Orkystra.Contracts.Transport;

public sealed record TransportExceptionResolutionHistoryReadModel(
    DateTimeOffset UpdatedAtUtc,
    int EntryCount,
    IReadOnlyCollection<TransportExceptionResolutionHistoryEntryReadModel> Entries);
