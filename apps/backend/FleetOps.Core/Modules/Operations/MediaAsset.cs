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
        long sizeBytes)
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
    }

    public string StorageKey { get; private init; } = string.Empty;
    public string FileName { get; private init; } = string.Empty;
    public string ContentType { get; private init; } = string.Empty;
    public long SizeBytes { get; private init; }

    private static string RequireNonEmpty(string value, string parameter) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Value is required.", parameter)
            : value.Trim();
}
