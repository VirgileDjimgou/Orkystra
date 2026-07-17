using FleetOps.Core.Common;

namespace FleetOps.Core.Modules.Operations;

public sealed class MediaAsset : TenantEntity
{
    private MediaAsset() { }

    public MediaAsset(
        Guid organizationId,
        string storageKey,
        string fileName,
        string contentType,
        long sizeBytes,
        string checksumSha256,
        DateTimeOffset retainUntilUtc)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("Organization is required.", nameof(organizationId));
        }

        if (sizeBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sizeBytes), "Media size must be positive.");
        }

        OrganizationId = organizationId;
        StorageKey = RequireNonEmpty(storageKey, nameof(storageKey));
        FileName = RequireNonEmpty(fileName, nameof(fileName));
        ContentType = RequireNonEmpty(contentType, nameof(contentType));
        SizeBytes = sizeBytes;
        ChecksumSha256 = RequireNonEmpty(checksumSha256, nameof(checksumSha256));
        RetainUntilUtc = retainUntilUtc.ToUniversalTime();
    }

    public string StorageKey { get; private set; } = string.Empty;
    public string FileName { get; private init; } = string.Empty;
    public string ContentType { get; private init; } = string.Empty;
    public long SizeBytes { get; private init; }
    public string ChecksumSha256 { get; private set; } = string.Empty;
    public DateTimeOffset RetainUntilUtc { get; private set; }
    public bool IsReadRevoked { get; private set; }
    public DateTimeOffset? ReadRevokedAtUtc { get; private set; }

    public void RevokeReadAccess(DateTimeOffset nowUtc)
    {
        IsReadRevoked = true;
        ReadRevokedAtUtc = nowUtc.ToUniversalTime();
    }

    public void RecordStorageMigration(string storageKey, string checksumSha256, DateTimeOffset retainUntilUtc)
    {
        if (!storageKey.StartsWith($"tenants/{OrganizationId:N}/media/", StringComparison.Ordinal))
            throw new ArgumentException("Storage key must belong to the media asset tenant.", nameof(storageKey));
        StorageKey = RequireNonEmpty(storageKey, nameof(storageKey));
        ChecksumSha256 = RequireNonEmpty(checksumSha256, nameof(checksumSha256));
        RetainUntilUtc = retainUntilUtc.ToUniversalTime();
    }

    private static string RequireNonEmpty(string value, string parameter) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Value is required.", parameter)
            : value.Trim();
}
