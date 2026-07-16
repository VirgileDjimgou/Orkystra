using FleetOps.Core.Common;

namespace FleetOps.Core.Modules.Integrations;

public sealed class WebhookEndpoint : TenantEntity
{
    private WebhookEndpoint() { }

    public WebhookEndpoint(
        Guid organizationId,
        string name,
        string eventType,
        string targetUrl,
        string signingSecret,
        bool isSandbox,
        Guid? id = null)
    {
        if (organizationId == Guid.Empty) throw new ArgumentException("Organization is required.", nameof(organizationId));
        if (id.HasValue && id.Value == Guid.Empty) throw new ArgumentException("Identifier cannot be empty.", nameof(id));
        if (id.HasValue)
        {
            Id = id.Value;
        }

        OrganizationId = organizationId;
        Name = RequireText(name, nameof(name), 120);
        EventType = RequireText(eventType, nameof(eventType), 80);
        TargetUrl = RequireUrl(targetUrl);
        SigningSecret = RequireText(signingSecret, nameof(signingSecret), 120);
        IsSandbox = isSandbox;
    }

    public string Name { get; private set; } = string.Empty;
    public string EventType { get; private set; } = string.Empty;
    public string TargetUrl { get; private set; } = string.Empty;
    public string SigningSecret { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;
    public bool IsSandbox { get; private set; }
    public DateTimeOffset? LastSucceededAtUtc { get; private set; }
    public DateTimeOffset? DisabledAtUtc { get; private set; }
    public long RowVersion { get; private set; }

    public void MarkSucceeded(DateTimeOffset succeededAtUtc)
    {
        LastSucceededAtUtc = succeededAtUtc.ToUniversalTime();
        RowVersion++;
    }

    public void Disable(DateTimeOffset disabledAtUtc)
    {
        if (!IsActive)
        {
            throw new InvalidOperationException("Webhook endpoint is already disabled.");
        }

        IsActive = false;
        DisabledAtUtc = disabledAtUtc.ToUniversalTime();
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

    private static string RequireUrl(string value)
    {
        var trimmed = RequireText(value, nameof(value), 280);
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out _))
        {
            throw new ArgumentException("Absolute URL is required.", nameof(value));
        }

        return trimmed;
    }
}
