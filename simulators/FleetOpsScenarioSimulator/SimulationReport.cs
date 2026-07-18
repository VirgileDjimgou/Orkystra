using System.Globalization;
using System.Text;
using System.Text.Json;

namespace FleetOps.Simulation;

public sealed record SimulationStep(
    string Tenant,
    string Actor,
    string Module,
    string Outcome,
    string Detail);

public sealed class SimulationReport
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly List<SimulationStep> steps = [];

    public SimulationReport(string runId, Uri apiBaseUrl)
    {
        RunId = runId;
        ApiBaseUrl = apiBaseUrl.ToString();
    }

    public static string Label => "SIMULATED DEVELOPMENT EVIDENCE — NOT PILOT OR COMMERCIAL PROOF";
    public string RunId { get; }
    public string ApiBaseUrl { get; }
    public DateTimeOffset GeneratedAtUtc { get; private set; }
    public IReadOnlyList<SimulationStep> Steps => steps;

    public void Add(string tenant, string actor, string module, string detail) =>
        steps.Add(new SimulationStep(tenant, actor, module, "PASSED", detail));

    public void Complete() => GeneratedAtUtc = DateTimeOffset.UtcNow;

    public async Task WriteAsync(string outputDirectory, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(outputDirectory);
        var jsonPath = Path.Combine(outputDirectory, "full-simulation-report.json");
        var markdownPath = Path.Combine(outputDirectory, "full-simulation-report.md");
        await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(this, SerializerOptions) + Environment.NewLine, cancellationToken);

        var markdown = new StringBuilder()
            .AppendLine("# FleetOps full simulation report")
            .AppendLine()
            .AppendLine(CultureInfo.InvariantCulture, $"> {Label}")
            .AppendLine()
            .AppendLine(CultureInfo.InvariantCulture, $"- Run: `{RunId}`")
            .AppendLine(CultureInfo.InvariantCulture, $"- Generated: `{GeneratedAtUtc:O}`")
            .AppendLine(CultureInfo.InvariantCulture, $"- API: `{ApiBaseUrl}`")
            .AppendLine()
            .AppendLine("| Tenant | Actor | Module | Outcome | Evidence |")
            .AppendLine("|---|---|---|---|---|");
        foreach (var step in steps)
        {
            markdown.AppendLine(CultureInfo.InvariantCulture, $"| {Escape(step.Tenant)} | {Escape(step.Actor)} | {Escape(step.Module)} | {step.Outcome} | {Escape(step.Detail)} |");
        }

        await File.WriteAllTextAsync(markdownPath, markdown.ToString(), cancellationToken);
    }

    private static string Escape(string value) => value.Replace("|", "\\|", StringComparison.Ordinal);
}
