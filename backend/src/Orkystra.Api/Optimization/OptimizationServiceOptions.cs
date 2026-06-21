namespace Orkystra.Api.Optimization;

public sealed class OptimizationServiceOptions
{
    public const string SectionName = "OptimizationService";

    public string BaseUrl { get; set; } = "http://127.0.0.1:8002";

    public int TimeoutSeconds { get; set; } = 8;
}
