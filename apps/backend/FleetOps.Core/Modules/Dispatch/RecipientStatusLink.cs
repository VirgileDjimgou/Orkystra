using FleetOps.Core.Common;

namespace FleetOps.Core.Modules.Dispatch;

public sealed class RecipientStatusLink : TenantEntity
{
    private RecipientStatusLink() { }

    public RecipientStatusLink(Guid organizationId, Guid missionId, string tokenHash, DateTimeOffset expiresAtUtc)
    {
        if (organizationId == Guid.Empty || missionId == Guid.Empty) throw new ArgumentException("Organization and mission are required.");
        if (string.IsNullOrWhiteSpace(tokenHash) || tokenHash.Length != 64) throw new ArgumentException("A SHA-256 token hash is required.", nameof(tokenHash));
        OrganizationId = organizationId;
        MissionId = missionId;
        TokenHash = tokenHash;
        ExpiresAtUtc = expiresAtUtc.ToUniversalTime();
    }

    public Guid MissionId { get; private init; }
    public string TokenHash { get; private init; } = string.Empty;
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }
    public DateTimeOffset? LastViewedAtUtc { get; private set; }
    public int ViewCount { get; private set; }

    public bool IsAvailableAt(DateTimeOffset now) => RevokedAtUtc is null && ExpiresAtUtc > now;
    public void Revoke(DateTimeOffset now) => RevokedAtUtc ??= now.ToUniversalTime();
    public void RecordView(DateTimeOffset now)
    {
        LastViewedAtUtc = now.ToUniversalTime();
        ViewCount++;
    }
}
