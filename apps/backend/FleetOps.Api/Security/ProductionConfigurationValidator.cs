using FleetOps.Infrastructure.Persistence;
using FleetOps.Infrastructure.Storage;

namespace FleetOps.Api.Security;

public static class ProductionConfigurationValidator
{
    private const string DevelopmentJwtKey = "FleetOps_LocalDevelopment_ChangeThisSigningKey_123456789";
    private const string DevelopmentMediaKey = "FleetOps_Dev_Signing_Key_Change_Me_123456789";
    private const string PilotMediaKey = "FleetOps_Pilot_Signing_Key_Change_Me_123456789";

    public static void Validate(IConfiguration configuration, IHostEnvironment environment)
    {
        if (!environment.IsProduction())
        {
            return;
        }

        var failures = new List<string>();
        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
        var storage = configuration.GetSection(ObjectStorageOptions.SectionName).Get<ObjectStorageOptions>()
            ?? new ObjectStorageOptions();
        var bootstrap = configuration.GetSection(BootstrapOptions.SectionName).Get<BootstrapOptions>()
            ?? new BootstrapOptions();
        var connectionString = configuration.GetConnectionString("FleetOps");

        ValidateSecret(jwt.SigningKey, DevelopmentJwtKey, "Jwt:SigningKey", failures);
        ValidateSecret(
            storage.MediaSigningKey,
            DevelopmentMediaKey,
            "ObjectStorage:MediaSigningKey",
            failures,
            PilotMediaKey);
        if (!string.Equals(storage.Provider, "S3", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(storage.ServiceUrl)
            || string.IsNullOrWhiteSpace(storage.BucketName)
            || string.IsNullOrWhiteSpace(storage.AccessKey)
            || storage.SecretKey.Length < 16
            || !Uri.TryCreate(storage.ServiceUrl, UriKind.Absolute, out var serviceUri)
            || (serviceUri.Scheme != Uri.UriSchemeHttp && serviceUri.Scheme != Uri.UriSchemeHttps))
        {
            failures.Add("ObjectStorage must use configured S3-compatible private storage in Production.");
        }
        if (storage.RetentionDays is < 1 or > 3650)
            failures.Add("ObjectStorage:RetentionDays must be between 1 and 3650 days.");

        if (string.IsNullOrWhiteSpace(connectionString)
            || connectionString.Contains("ChangeThis_LocalOnly", StringComparison.OrdinalIgnoreCase))
        {
            failures.Add("ConnectionStrings:FleetOps must be explicitly configured for Production.");
        }

        if (bootstrap.SeedDemoData)
        {
            failures.Add("Bootstrap:SeedDemoData cannot be enabled in Production.");
        }

        if (failures.Count > 0)
        {
            throw new InvalidOperationException(
                "Unsafe FleetOps Production configuration: " + string.Join(" ", failures));
        }
    }

    private static void ValidateSecret(
        string value,
        string knownDevelopmentValue,
        string settingName,
        List<string> failures,
        params string[] additionalKnownValues)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length < 32
            || string.Equals(value, knownDevelopmentValue, StringComparison.Ordinal)
            || additionalKnownValues.Contains(value, StringComparer.Ordinal))
        {
            failures.Add($"{settingName} must be an independent secret of at least 32 characters.");
        }
    }
}
