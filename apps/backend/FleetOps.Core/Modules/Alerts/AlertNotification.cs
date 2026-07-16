using FleetOps.Core.Common;

namespace FleetOps.Core.Modules.Alerts;

public sealed class AlertNotification : TenantEntity
{
    private AlertNotification() { }

    public AlertNotification(
        Guid organizationId,
        Guid alertId,
        AlertNotificationChannel channel,
        string subject,
        string body,
        DateTimeOffset sentAtUtc)
    {
        if (organizationId == Guid.Empty) throw new ArgumentException("Organization is required.", nameof(organizationId));
        if (alertId == Guid.Empty) throw new ArgumentException("Alert is required.", nameof(alertId));

        OrganizationId = organizationId;
        AlertId = alertId;
        Channel = channel;
        Subject = RequireText(subject, nameof(subject), 160);
        Body = RequireText(body, nameof(body), 800);
        SentAtUtc = sentAtUtc.ToUniversalTime();
    }

    public Guid AlertId { get; private set; }
    public AlertNotificationChannel Channel { get; private set; }
    public string Subject { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public DateTimeOffset SentAtUtc { get; private set; }

    private static string RequireText(string value, string parameterName, int maxLength)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            throw new ArgumentException("Value is required.", parameterName);
        }

        return trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
    }
}
