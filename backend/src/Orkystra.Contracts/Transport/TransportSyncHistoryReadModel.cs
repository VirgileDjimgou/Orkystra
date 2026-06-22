namespace Orkystra.Contracts.Transport;

public sealed record TransportSyncHistoryReadModel(
    int Count,
    string Summary,
    IReadOnlyCollection<TransportSyncHistoryEntryReadModel> Entries);
