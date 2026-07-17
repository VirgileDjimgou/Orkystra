using FleetOps.Core.Common;

namespace FleetOps.Core.Modules.Operations;

public sealed class MediaUploadSession : TenantEntity
{
    private MediaUploadSession() { }

    public MediaUploadSession(
        Guid organizationId,
        Guid driverId,
        MediaUploadPurpose purpose,
        string fileName,
        string contentType,
        long totalBytes,
        DateTimeOffset expiresAtUtc,
        string tempStorageKey)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("Organization is required.", nameof(organizationId));
        }

        if (driverId == Guid.Empty)
        {
            throw new ArgumentException("Driver is required.", nameof(driverId));
        }

        if (totalBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalBytes), "Total bytes must be positive.");
        }

        OrganizationId = organizationId;
        DriverId = driverId;
        Purpose = purpose;
        FileName = RequireNonEmpty(fileName, nameof(fileName));
        ContentType = RequireNonEmpty(contentType, nameof(contentType));
        TotalBytes = totalBytes;
        UploadedBytes = 0;
        ExpiresAtUtc = expiresAtUtc.ToUniversalTime();
        TempStorageKey = RequireNonEmpty(tempStorageKey, nameof(tempStorageKey));
    }

    public Guid DriverId { get; private init; }
    public MediaUploadPurpose Purpose { get; private init; }
    public string FileName { get; private init; } = string.Empty;
    public string ContentType { get; private init; } = string.Empty;
    public long TotalBytes { get; private init; }
    public long UploadedBytes { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private init; }
    public string TempStorageKey { get; private init; } = string.Empty;
    public bool IsCompleted { get; private set; }
    public Guid? MediaAssetId { get; private set; }
    public UploadedContentDisposition? ScanDisposition { get; private set; }
    public string? ScanReason { get; private set; }

    public void RecordScan(UploadedContentScanResult result)
    {
        if (IsCompleted)
        {
            throw new InvalidOperationException("Completed uploads cannot be rescanned.");
        }

        ScanDisposition = result.Disposition;
        ScanReason = result.Reason.Length <= 240 ? result.Reason : result.Reason[..240];
    }

    public void Advance(long bytesUploaded)
    {
        if (bytesUploaded <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bytesUploaded), "Uploaded bytes must be positive.");
        }

        if (IsCompleted)
        {
            throw new InvalidOperationException("Upload session is already completed.");
        }

        UploadedBytes += bytesUploaded;
        if (UploadedBytes > TotalBytes)
        {
            throw new InvalidOperationException("Upload session cannot exceed its declared size.");
        }
    }

    public void Complete(Guid mediaAssetId)
    {
        if (mediaAssetId == Guid.Empty)
        {
            throw new ArgumentException("Media asset is required.", nameof(mediaAssetId));
        }

        if (UploadedBytes != TotalBytes)
        {
            throw new InvalidOperationException("Upload session cannot be completed before all bytes are uploaded.");
        }

        if (ScanDisposition != UploadedContentDisposition.Clean)
        {
            throw new InvalidOperationException("Upload content must pass security scanning before completion.");
        }

        IsCompleted = true;
        MediaAssetId = mediaAssetId;
    }

    private static string RequireNonEmpty(string value, string parameter) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Value is required.", parameter)
            : value.Trim();
}
