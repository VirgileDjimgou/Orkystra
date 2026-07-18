using System.Globalization;
using FleetOps.Simulation;

var options = ParseArguments(args);
using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(5));
using var http = new HttpClient
{
    BaseAddress = options.ApiBaseUrl,
    Timeout = TimeSpan.FromSeconds(30),
};

var selectedTenants = string.IsNullOrWhiteSpace(options.TenantSlug)
    ? SimulationCatalog.Tenants
    : SimulationCatalog.Tenants.Where(x => x.Slug.Equals(options.TenantSlug, StringComparison.OrdinalIgnoreCase)).ToList();
if (selectedTenants.Count == 0)
{
    Console.Error.WriteLine($"Unknown tenant '{options.TenantSlug}'. Expected: {string.Join(", ", SimulationCatalog.Tenants.Select(x => x.Slug))}.");
    return 2;
}

var report = new SimulationReport(options.RunId, options.ApiBaseUrl);
var runner = new FullFleetScenario(new FleetOpsSimulationClient(http), report);
try
{
    Console.WriteLine(SimulationReport.Label);
    Console.WriteLine($"API: {options.ApiBaseUrl}; tenants: {string.Join(", ", selectedTenants.Select(x => x.Slug))}; run: {options.RunId}");
    await runner.RunAsync(selectedTenants, cancellation.Token);
    await report.WriteAsync(options.OutputDirectory, cancellation.Token);
    foreach (var step in report.Steps)
    {
        Console.WriteLine($"PASS [{step.Tenant}] {step.Actor} / {step.Module}: {step.Detail}");
    }

    Console.WriteLine($"Simulation passed with {report.Steps.Count} evidence steps. Reports: {Path.GetFullPath(options.OutputDirectory)}");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Simulation failed: {exception.Message}");
    return 1;
}

static SimulatorOptions ParseArguments(string[] arguments)
{
    string? ValueAfter(string name)
    {
        var index = Array.FindIndex(arguments, x => x.Equals(name, StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : null;
    }

    var rawUrl = ValueAfter("--api-url") ?? Environment.GetEnvironmentVariable("FLEETOPS_API_URL") ?? "http://127.0.0.1:5080";
    if (!Uri.TryCreate(rawUrl, UriKind.Absolute, out var apiBaseUrl))
    {
        throw new ArgumentException($"Invalid API URL: {rawUrl}");
    }

    return new SimulatorOptions(
        apiBaseUrl,
        ValueAfter("--output") ?? Path.Combine(".runtime", "simulation"),
        ValueAfter("--run-id") ?? DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture),
        ValueAfter("--tenant"));
}

internal sealed record SimulatorOptions(Uri ApiBaseUrl, string OutputDirectory, string RunId, string? TenantSlug);
