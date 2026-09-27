using FleetOps.Api.Demo;
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
        var publicDemo = configuration.GetSection(PublicDemoOptions.SectionName).Get<PublicDemoOptions>()
            ?? new PublicDemoOptions();
        var bootstrap = configuration.GetSection(BootstrapOptions.SectionName).Get<BootstrapOptions>()
            ?? new BootstrapOptions();

        if (environment.IsEnvironment("Demo") || environment.IsEnvironment("DemoTesting"))
        {
            var demoFailures = new List<string>();
            if (!publicDemo.Enabled) demoFailures.Add("PublicDemo:Enabled must be true in Demo.");
            if (!publicDemo.SideEffectsSandboxed) demoFailures.Add("PublicDemo:SideEffectsSandboxed must be true in Demo.");
            if (!bootstrap.SeedDemoData) demoFailures.Add("Bootstrap:SeedDemoData must be true in Demo.");
            if (environment.IsEnvironment("Demo") && !bootstrap.PublicDemoOnly)
                demoFailures.Add("Bootstrap:PublicDemoOnly must be true in Demo.");
            if (publicDemo.SessionLifetimeSeconds is < 1 or > 1800) demoFailures.Add("PublicDemo:SessionLifetimeSeconds must be between 1 and 1800.");
            if (publicDemo.LaunchPermitLimit is < 1 or > 100) demoFailures.Add("PublicDemo:LaunchPermitLimit must be between 1 and 100.");
            if (publicDemo.MaxConcurrentSessions is < 1 or > 100) demoFailures.Add("PublicDemo:MaxConcurrentSessions must be between 1 and 100.");
            if (demoFailures.Count > 0)
                throw new InvalidOperationException("Unsafe FleetOps Demo configuration: " + string.Join(" ", demoFailures));
            return;
        }

        if (!environment.IsProduction())
        {
            if (publicDemo.Enabled)
                throw new InvalidOperationException("PublicDemo:Enabled is only valid in the Demo environment.");
            return;
        }

        var failures = new List<string>();
        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
        var storage = configuration.GetSection(ObjectStorageOptions.SectionName).Get<ObjectStorageOptions>()
            ?? new ObjectStorageOptions();
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
        if (publicDemo.Enabled)
        {
            failures.Add("PublicDemo:Enabled cannot be enabled in Production.");
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
