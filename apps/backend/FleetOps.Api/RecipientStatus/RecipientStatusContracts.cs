using FleetOps.Core.Modules.Dispatch;

namespace FleetOps.Api.RecipientStatus;

public sealed record CreateRecipientStatusLinkRequest(DateTimeOffset ExpiresAtUtc);
public sealed record RecipientStatusLinkResponse(Guid Id, string Url, DateTimeOffset ExpiresAtUtc);
public sealed record PublicRecipientStatusResponse(string Status, string EtaWindow, DateTimeOffset LastUpdatedUtc, bool TrackingAvailable, bool Delivered);
public sealed record UpdateRecipientNotificationPreferenceRequest(bool ConsentGranted, RecipientNotificationChannel Channel, string Language, string TimeZoneId, int? QuietHoursStartHour, int? QuietHoursEndHour);
public sealed record RecipientNotificationPreferenceResponse(bool ConsentGranted, RecipientNotificationChannel Channel, string Language, string TimeZoneId, int? QuietHoursStartHour, int? QuietHoursEndHour);
public sealed record CorrectRecipientContactRequest(string Email);
public sealed record RecipientStatusMetricsResponse(int ActiveLinks, int UsefulViews, int NotificationsDelivered, int NotificationsDeadLettered);
