namespace FleetOps.Infrastructure.Storage;

public sealed class ObjectStorageOptions
{
    public const string SectionName = "ObjectStorage";

    public string RootPath { get; set; } = ".runtime/private-media";
    public string MediaSigningKey { get; set; } = "FleetOps_Dev_Signing_Key_Change_Me_123456789";
}
