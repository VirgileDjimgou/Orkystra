namespace Orkystra.Api.Connectors;

public static class ProviderRuntimeMetadata
{
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
        return RequiredFieldsByProvider.TryGetValue(providerId, out var fields)
            ? fields
            : [];
    }

    public static IReadOnlyCollection<string> GetRequiredFields(string providerId)
    {
        return GetEditableFields(providerId);
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
}
