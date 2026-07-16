using FleetOps.Core.Common;

namespace FleetOps.Core.Modules.Integrations;

public sealed class SandboxWebhookReceipt : TenantEntity
{
    private SandboxWebhookReceipt() { }

    public SandboxWebhookReceipt(
        Guid organizationId,
        Guid webhookEndpointId,
        string eventType,
        string signature,
        string payloadJson,
        DateTimeOffset receivedAtUtc)
    {
        if (organizationId == Guid.Empty) throw new ArgumentException("Organization is required.", nameof(organizationId));
        if (webhookEndpointId == Guid.Empty) throw new ArgumentException("Webhook endpoint is required.", nameof(webhookEndpointId));
        OrganizationId = organizationId;
        WebhookEndpointId = webhookEndpointId;
        EventType = RequireText(eventType, nameof(eventType), 80);
        Signature = RequireText(signature, nameof(signature), 180);
        PayloadJson = RequireText(payloadJson, nameof(payloadJson), 4000);
        ReceivedAtUtc = receivedAtUtc.ToUniversalTime();
    }

    public Guid WebhookEndpointId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string Signature { get; private set; } = string.Empty;
    public string PayloadJson { get; private set; } = string.Empty;
    public DateTimeOffset ReceivedAtUtc { get; private set; }

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
}
