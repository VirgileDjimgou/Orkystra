using FleetOps.Core.Common;

namespace FleetOps.Core.Modules.Integrations;

public sealed class IntegrationOutboxMessage : TenantEntity
{
    private IntegrationOutboxMessage() { }

    public IntegrationOutboxMessage(
        Guid organizationId,
        Guid webhookEndpointId,
        string eventType,
        string aggregateType,
        string aggregateId,
        string payloadJson,
        DateTimeOffset occurredAtUtc)
    {
        if (organizationId == Guid.Empty) throw new ArgumentException("Organization is required.", nameof(organizationId));
        if (webhookEndpointId == Guid.Empty) throw new ArgumentException("Webhook endpoint is required.", nameof(webhookEndpointId));
        OrganizationId = organizationId;
        WebhookEndpointId = webhookEndpointId;
        EventType = RequireText(eventType, nameof(eventType), 80);
        AggregateType = RequireText(aggregateType, nameof(aggregateType), 64);
        AggregateId = RequireText(aggregateId, nameof(aggregateId), 80);
        PayloadJson = RequireText(payloadJson, nameof(payloadJson), 4000);
        OccurredAtUtc = occurredAtUtc.ToUniversalTime();
        Status = IntegrationOutboxStatus.Pending;
        NextAttemptAtUtc = OccurredAtUtc;
    }

    public Guid WebhookEndpointId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string AggregateType { get; private set; } = string.Empty;
    public string AggregateId { get; private set; } = string.Empty;
    public string PayloadJson { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAtUtc { get; private set; }
    public IntegrationOutboxStatus Status { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTimeOffset NextAttemptAtUtc { get; private set; }
    public DateTimeOffset? DeliveredAtUtc { get; private set; }
    public DateTimeOffset? DeadLetteredAtUtc { get; private set; }
    public string? LastError { get; private set; }
    public long RowVersion { get; private set; }

    public void ScheduleRetry(DateTimeOffset nextAttemptAtUtc, string error)
    {
        AttemptCount++;
        NextAttemptAtUtc = nextAttemptAtUtc.ToUniversalTime();
        LastError = NormalizeOptional(error, 500);
        RowVersion++;
    }

    public void MarkDelivered(DateTimeOffset deliveredAtUtc)
    {
        Status = IntegrationOutboxStatus.Delivered;
        DeliveredAtUtc = deliveredAtUtc.ToUniversalTime();
        AttemptCount++;
        RowVersion++;
    }

    public void MarkDeadLetter(DateTimeOffset deadLetteredAtUtc, string error)
    {
        Status = IntegrationOutboxStatus.DeadLetter;
        DeadLetteredAtUtc = deadLetteredAtUtc.ToUniversalTime();
        AttemptCount++;
        LastError = NormalizeOptional(error, 500);
        RowVersion++;
    }

    private static string RequireText(string value, string parameterName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", parameterName);
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }

        return trimmed;
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
    }
}
