namespace Orkystra.Contracts.Transport;

public sealed record TransportExceptionWorkbenchReadModel(
    DateTimeOffset GeneratedAtUtc,
    int ExceptionCount,
    string Summary,
    IReadOnlyCollection<TransportExceptionWorkbenchGroupReadModel> Groups,
    IReadOnlyCollection<TransportExceptionWorkbenchItemReadModel> Items);
