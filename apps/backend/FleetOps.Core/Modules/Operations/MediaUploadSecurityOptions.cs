namespace FleetOps.Core.Modules.Operations;

public sealed class MediaUploadSecurityOptions
{
    public const string SectionName = "Security:Uploads";

    public long MaximumBytes { get; init; } = 10 * 1024 * 1024;
    public bool MalwareSignatureScanEnabled { get; init; } = true;
}
