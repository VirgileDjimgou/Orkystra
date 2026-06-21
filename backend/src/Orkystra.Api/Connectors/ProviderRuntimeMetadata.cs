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
}
