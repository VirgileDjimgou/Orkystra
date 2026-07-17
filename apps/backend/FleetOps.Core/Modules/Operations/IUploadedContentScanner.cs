namespace FleetOps.Core.Modules.Operations;

public enum UploadedContentDisposition
{
    Clean = 1,
    Quarantine = 2,
}

public sealed record UploadedContentScanResult(
    UploadedContentDisposition Disposition,
    string DetectedContentType,
    string Reason);

public interface IUploadedContentScanner
{
    Task<UploadedContentScanResult> ScanAsync(
        Stream content,
        string claimedContentType,
        CancellationToken cancellationToken);
}
