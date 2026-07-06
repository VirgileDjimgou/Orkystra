namespace Orkystra.Api.Connectors;

public static class ProviderRuntimeMetadata
{
    private static readonly IReadOnlyDictionary<string, string[]> EditableFieldsByProvider =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["csv-warehouse-import"] = ["sourcePath", "importSchedule"],
            ["rest-transport-adapter"] = ["baseUrl", "authMode", "writebackMode"],
            ["gps-telematics-adapter"] = ["streamTopic", "snapshotIntervalSeconds"]
        };

    private static readonly IReadOnlyDictionary<string, string[]> RequiredFieldsByProvider =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["csv-warehouse-import"] = ["sourcePath", "importSchedule"],
            ["rest-transport-adapter"] = ["baseUrl", "authMode"],
            ["gps-telematics-adapter"] = ["streamTopic", "snapshotIntervalSeconds"]
        };

    // Secret fields are stored and managed separately from regular settings.
    // They must never be serialised into appsettings files or returned as values in API responses.
    private static readonly IReadOnlyDictionary<string, string[]> SecretFieldsByProvider =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["rest-transport-adapter"] = ["apiKey"]
        };

    public static bool IsKnownProvider(string providerId)
    {
        return RequiredFieldsByProvider.ContainsKey(providerId);
    }

    public static IReadOnlyCollection<string> GetEditableFields(string providerId)
    {
        return EditableFieldsByProvider.TryGetValue(providerId, out var fields)
            ? fields
            : [];
    }

    public static IReadOnlyCollection<string> GetRequiredFields(string providerId)
    {
        return RequiredFieldsByProvider.TryGetValue(providerId, out var fields)
            ? fields
            : [];
    }

    public static IReadOnlyCollection<string> GetSecretFields(string providerId)
    {
        return SecretFieldsByProvider.TryGetValue(providerId, out var fields)
            ? fields
            : [];
    }

    public static bool IsSecretField(string providerId, string fieldKey)
    {
        return SecretFieldsByProvider.TryGetValue(providerId, out var fields)
            && fields.Contains(fieldKey, StringComparer.OrdinalIgnoreCase);
    }

    public static string GetAuthMode(ProviderRuntimeSettings? settings)
    {
        if (settings is null)
        {
            return "none";
        }

        return settings.Settings.TryGetValue("authMode", out var mode) && !string.IsNullOrWhiteSpace(mode)
            ? mode.Trim()
            : "none";
    }

    public static bool SupportsWriteback(string providerId)
    {
        return string.Equals(providerId, "rest-transport-adapter", StringComparison.OrdinalIgnoreCase);
    }

    public static string GetWritebackMode(string providerId, ProviderRuntimeSettings? settings)
    {
        if (!SupportsWriteback(providerId))
        {
            return "read-only";
        }

        if (settings is null)
        {
            return "dry-run";
        }

        if (!settings.Settings.TryGetValue("writebackMode", out var mode) || string.IsNullOrWhiteSpace(mode))
        {
            return "dry-run";
        }

        return NormalizeWritebackMode(mode);
    }

    public static bool IsWritebackModeValid(string providerId, string mode)
    {
        if (!SupportsWriteback(providerId))
        {
            return string.Equals(mode, "read-only", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mode, "disabled", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(mode);
        }

        return string.Equals(mode, "disabled", StringComparison.OrdinalIgnoreCase)
            || string.Equals(mode, "dry-run", StringComparison.OrdinalIgnoreCase)
            || string.Equals(mode, "enabled", StringComparison.OrdinalIgnoreCase);
    }

    public static string NormalizeWritebackMode(string mode)
    {
        return mode.Trim().ToLowerInvariant() switch
        {
            "enabled" => "enabled",
            "disabled" => "disabled",
            _ => "dry-run"
        };
    }
}
