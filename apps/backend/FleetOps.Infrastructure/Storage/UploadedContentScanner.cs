using System.Text;
using FleetOps.Core.Modules.Operations;
using Microsoft.Extensions.Options;

namespace FleetOps.Infrastructure.Storage;

public sealed class UploadedContentScanner(IOptions<MediaUploadSecurityOptions> options) : IUploadedContentScanner
{
    private static readonly byte[] EicarMarker = Encoding.ASCII.GetBytes("EICAR-STANDARD-ANTIVIRUS-TEST-FILE");
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];

    public async Task<UploadedContentScanResult> ScanAsync(
        Stream content,
        string claimedContentType,
        CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        if (buffer.Length <= 0 || buffer.Length > Math.Max(1, options.Value.MaximumBytes))
        {
            return Quarantine("The uploaded content is empty or exceeds the scanner limit.");
        }

        var bytes = buffer.ToArray();
        if (options.Value.MalwareSignatureScanEnabled && bytes.AsSpan().IndexOf(EicarMarker) >= 0)
        {
            return Quarantine("The uploaded content matched the configured malware-test signature.");
        }

        var detectedType = DetectContentType(bytes);
        if (detectedType is null)
        {
            return Quarantine("The uploaded content signature is not an accepted image format.");
        }

        if (!string.Equals(detectedType, claimedContentType, StringComparison.OrdinalIgnoreCase))
        {
            return new UploadedContentScanResult(
                UploadedContentDisposition.Quarantine,
                detectedType,
                "The declared content type does not match the file signature.");
        }

        return new UploadedContentScanResult(UploadedContentDisposition.Clean, detectedType, "Signature and malware checks passed.");
    }

    private static string? DetectContentType(ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith(PngSignature))
        {
            return "image/png";
        }

        if (bytes.StartsWith(JpegSignature))
        {
            return "image/jpeg";
        }

        return null;
    }

    private static UploadedContentScanResult Quarantine(string reason) =>
        new(UploadedContentDisposition.Quarantine, "application/octet-stream", reason);
}
