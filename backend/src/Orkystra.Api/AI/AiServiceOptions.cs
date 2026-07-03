namespace Orkystra.Api.AI;

public sealed class AiServiceOptions
{
    public const string SectionName = "AiService";

    /// <summary>
    /// AI provider to use: "http" (Python service, falls back to local), "local" (deterministic only), or "disabled".
    /// Default is "http" for backward compatibility.
    /// </summary>
    public string Provider { get; set; } = "http";

    public string BaseUrl { get; set; } = "http://127.0.0.1:8001";

    public int TimeoutSeconds { get; set; } = 8;
}
