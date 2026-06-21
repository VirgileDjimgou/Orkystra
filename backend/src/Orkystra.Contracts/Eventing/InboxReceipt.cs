namespace Orkystra.Contracts.Eventing;

public sealed record InboxReceipt(
    Guid MessageId,
    string ConsumerName,
    DateTimeOffset ReceivedAt,
    DateTimeOffset? ProcessedAt,
    string Status);
