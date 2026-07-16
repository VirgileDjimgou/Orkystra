using FleetOps.Core.Common;

namespace FleetOps.Core.Modules.Integrations;

public sealed class WebhookDeliveryAttempt : TenantEntity
{
    private WebhookDeliveryAttempt() { }

    public WebhookDeliveryAttempt(
        Guid organizationId,
        Guid outboxMessageId,
        Guid webhookEndpointId,
        int attemptNumber,
        int? responseStatusCode,
        string? responseBody,
        bool isSuccess,
        DateTimeOffset attemptedAtUtc)
    {
        if (organizationId == Guid.Empty) throw new ArgumentException("Organization is required.", nameof(organizationId));
        if (outboxMessageId == Guid.Empty) throw new ArgumentException("Outbox message is required.", nameof(outboxMessageId));
        if (webhookEndpointId == Guid.Empty) throw new ArgumentException("Webhook endpoint is required.", nameof(webhookEndpointId));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(attemptNumber);

        OrganizationId = organizationId;
        OutboxMessageId = outboxMessageId;
        WebhookEndpointId = webhookEndpointId;
        AttemptNumber = attemptNumber;
        ResponseStatusCode = responseStatusCode;
        ResponseBody = NormalizeOptional(responseBody, 1000);
        IsSuccess = isSuccess;
        AttemptedAtUtc = attemptedAtUtc.ToUniversalTime();
    }

    public Guid OutboxMessageId { get; private set; }
    public Guid WebhookEndpointId { get; private set; }
    public int AttemptNumber { get; private set; }
    public int? ResponseStatusCode { get; private set; }
    public string? ResponseBody { get; private set; }
    public bool IsSuccess { get; private set; }
    public DateTimeOffset AttemptedAtUtc { get; private set; }

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
