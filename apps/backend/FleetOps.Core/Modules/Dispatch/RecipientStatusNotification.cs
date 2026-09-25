using FleetOps.Core.Common;

namespace FleetOps.Core.Modules.Dispatch;

public enum RecipientStatusNotificationDeliveryStatus { Pending, Delivered, DeadLetter }

public sealed class RecipientStatusNotification : TenantEntity
{
    private RecipientStatusNotification() { }

    public RecipientStatusNotification(Guid organizationId, Guid recipientStatusLinkId, Guid missionId, RecipientNotificationChannel channel, string status, string deduplicationKey, DateTimeOffset occurredAtUtc)
    {
        if (organizationId == Guid.Empty || recipientStatusLinkId == Guid.Empty || missionId == Guid.Empty) throw new ArgumentException("Organization, link and mission are required.");
        if (string.IsNullOrWhiteSpace(status) || status.Length > 40) throw new ArgumentException("Status is required.", nameof(status));
        if (string.IsNullOrWhiteSpace(deduplicationKey) || deduplicationKey.Length > 160) throw new ArgumentException("Deduplication key is required.", nameof(deduplicationKey));
        OrganizationId = organizationId;
        RecipientStatusLinkId = recipientStatusLinkId;
        MissionId = missionId;
        Channel = channel;
        Status = status.Trim();
        DeduplicationKey = deduplicationKey.Trim();
        OccurredAtUtc = occurredAtUtc.ToUniversalTime();
        NextAttemptAtUtc = OccurredAtUtc;
    }

    public Guid RecipientStatusLinkId { get; private set; }
    public Guid MissionId { get; private set; }
    public RecipientNotificationChannel Channel { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public string DeduplicationKey { get; private set; } = string.Empty;
    public RecipientStatusNotificationDeliveryStatus DeliveryStatus { get; private set; } = RecipientStatusNotificationDeliveryStatus.Pending;
    public int AttemptCount { get; private set; }
    public DateTimeOffset OccurredAtUtc { get; private set; }
    public DateTimeOffset NextAttemptAtUtc { get; private set; }
    public DateTimeOffset? DeliveredAtUtc { get; private set; }
    public DateTimeOffset? DeadLetteredAtUtc { get; private set; }
    public string? LastError { get; private set; }

    public void RetryAt(DateTimeOffset nextAttemptAtUtc, string error) { AttemptCount++; NextAttemptAtUtc = nextAttemptAtUtc.ToUniversalTime(); LastError = Normalize(error); }
    public void MarkDelivered(DateTimeOffset now) { AttemptCount++; DeliveryStatus = RecipientStatusNotificationDeliveryStatus.Delivered; DeliveredAtUtc = now.ToUniversalTime(); LastError = null; }
    public void MarkDeadLetter(DateTimeOffset now, string error) { AttemptCount++; DeliveryStatus = RecipientStatusNotificationDeliveryStatus.DeadLetter; DeadLetteredAtUtc = now.ToUniversalTime(); LastError = Normalize(error); }
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(500, value.Trim().Length)];
}
