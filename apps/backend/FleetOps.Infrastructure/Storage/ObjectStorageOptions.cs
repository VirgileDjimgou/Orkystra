namespace FleetOps.Infrastructure.Storage;

public sealed class ObjectStorageOptions
{
    public const string SectionName = "ObjectStorage";

    public string RootPath { get; set; } = ".runtime/private-media";
    public string MediaSigningKey { get; set; } = "FleetOps_Dev_Signing_Key_Change_Me_123456789";
    public string Provider { get; set; } = "FileSystem";
    public string ServiceUrl { get; set; } = string.Empty;
    public string BucketName { get; set; } = "fleetops-private-media";
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public int RetentionDays { get; set; } = 365;
}
