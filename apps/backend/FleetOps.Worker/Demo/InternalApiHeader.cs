namespace FleetOps.Worker.Demo;

internal static class InternalApiHeader
{
    public const string Name = "X-FleetOps-Internal-Key";

    public static void Apply(HttpClient client, string? key)
    {
        if (!string.IsNullOrWhiteSpace(key))
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation(Name, key);
        }
    }
}
