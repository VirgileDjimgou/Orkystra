namespace FleetOps.Core.Modules.Identity;

public sealed class UserSession
{
    private UserSession() { }

    public UserSession(
        Guid organizationId,
        Guid userId,
        string clientType,
        DateTimeOffset expiresAtUtc)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("Organization is required.", nameof(organizationId));
        }

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User is required.", nameof(userId));
        }

        if (expiresAtUtc <= DateTimeOffset.UtcNow)
        {
            throw new ArgumentOutOfRangeException(nameof(expiresAtUtc), "Session expiry must be in the future.");
        }

        OrganizationId = organizationId;
        UserId = userId;
        ClientType = string.IsNullOrWhiteSpace(clientType)
            ? "unknown"
            : clientType.Trim().ToLowerInvariant();
        ExpiresAtUtc = expiresAtUtc.ToUniversalTime();
    }

    public Guid Id { get; private init; } = Guid.NewGuid();
    public Guid OrganizationId { get; private init; }
    public Guid UserId { get; private init; }
    public string ClientType { get; private init; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAtUtc { get; private init; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }
    public Guid? RevokedByUserId { get; private set; }
    public string? RevocationReason { get; private set; }

    public bool IsActive(DateTimeOffset now) => RevokedAtUtc is null && ExpiresAtUtc > now;

    public void Revoke(Guid revokedByUserId, string reason, DateTimeOffset revokedAtUtc)
    {
        if (RevokedAtUtc is not null)
        {
            return;
        }

        RevokedAtUtc = revokedAtUtc.ToUniversalTime();
        RevokedByUserId = revokedByUserId == Guid.Empty ? null : revokedByUserId;
        RevocationReason = string.IsNullOrWhiteSpace(reason) ? "revoked" : reason.Trim();
    }
}
