using FleetOps.Core.Modules.Dispatch;

namespace FleetOps.Api.RecipientStatus;

public sealed record CreateRecipientStatusLinkRequest(DateTimeOffset ExpiresAtUtc);
public sealed record RecipientStatusLinkResponse(Guid Id, string Url, DateTimeOffset ExpiresAtUtc);
public sealed record PublicRecipientStatusResponse(string Status, string EtaWindow, DateTimeOffset LastUpdatedUtc, bool TrackingAvailable, bool Delivered);
