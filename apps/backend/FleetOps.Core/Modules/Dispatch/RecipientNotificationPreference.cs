using FleetOps.Core.Common;

namespace FleetOps.Core.Modules.Dispatch;

public enum RecipientNotificationChannel { Email }

public sealed class RecipientNotificationPreference : TenantEntity
{
    private RecipientNotificationPreference() { }

    public RecipientNotificationPreference(Guid organizationId)
    {
        if (organizationId == Guid.Empty) throw new ArgumentException("Organization is required.", nameof(organizationId));
        OrganizationId = organizationId;
    }

    public bool ConsentGranted { get; private set; }
    public RecipientNotificationChannel Channel { get; private set; } = RecipientNotificationChannel.Email;
    public string Language { get; private set; } = "en";
    public string TimeZoneId { get; private set; } = "UTC";
    public int? QuietHoursStartHour { get; private set; }
    public int? QuietHoursEndHour { get; private set; }

    public void Update(bool consentGranted, RecipientNotificationChannel channel, string language, string timeZoneId, int? quietHoursStartHour, int? quietHoursEndHour)
    {
        if (string.IsNullOrWhiteSpace(language) || language.Trim().Length > 16) throw new ArgumentException("Language is required and must not exceed 16 characters.", nameof(language));
        if (string.IsNullOrWhiteSpace(timeZoneId) || timeZoneId.Trim().Length > 128) throw new ArgumentException("Time zone is required and must not exceed 128 characters.", nameof(timeZoneId));
        if (quietHoursStartHour is < 0 or > 23 || quietHoursEndHour is < 0 or > 23 || quietHoursStartHour.HasValue != quietHoursEndHour.HasValue)
            throw new ArgumentOutOfRangeException(nameof(quietHoursStartHour), "Quiet hours must be two whole hours between 0 and 23.");

        ConsentGranted = consentGranted;
        Channel = channel;
        Language = language.Trim().ToLowerInvariant();
        TimeZoneId = timeZoneId.Trim();
        QuietHoursStartHour = quietHoursStartHour;
        QuietHoursEndHour = quietHoursEndHour;
    }

    public bool IsQuietAt(DateTimeOffset utcNow)
    {
        if (QuietHoursStartHour is null || QuietHoursEndHour is null || QuietHoursStartHour == QuietHoursEndHour) return false;
        var localHour = TimeZoneInfo.ConvertTime(utcNow, TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId)).Hour;
        return QuietHoursStartHour < QuietHoursEndHour
            ? localHour >= QuietHoursStartHour && localHour < QuietHoursEndHour
            : localHour >= QuietHoursStartHour || localHour < QuietHoursEndHour;
    }
}
