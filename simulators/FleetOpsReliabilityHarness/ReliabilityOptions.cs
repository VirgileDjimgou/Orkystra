using System.Globalization;

namespace FleetOpsReliabilityHarness;

public sealed class ReliabilityOptions
{
    public string ApiBaseUrl { get; private init; } = "http://localhost:5091";
    public string OrganizationSlug { get; private init; } = "northwind";
    public int Vehicles { get; private init; } = 20;
    public int DurationSeconds { get; private init; } = 900;
    public int IntervalMilliseconds { get; private init; } = 5000;
    public int SnapshotEverySeconds { get; private init; } = 30;
    public int GraceSeconds { get; private init; } = 5;
    public int InjectDuplicatesEvery { get; private init; }
    public int InjectOutOfOrderEvery { get; private init; }
    public bool SignalR { get; private init; } = true;
    public string Mode { get; private init; } = "load";
    public string RunId { get; private init; } = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
    public string OutputPath { get; private init; } = string.Empty;
    public string OperatorEmail { get; private init; } = "operator@northwind.local";
    public string OperatorPassword { get; private init; } = "Operator123!";

    public static ReliabilityOptions Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            if (!argument.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            var key = argument[2..];
            var value = index + 1 < args.Length && !args[index + 1].StartsWith("--", StringComparison.Ordinal)
                ? args[++index]
                : "true";
            values[key] = value;
        }

        var mode = Get(values, "mode", "load").Trim().ToLowerInvariant();
        if (mode is not ("load" or "observe"))
        {
            throw new ArgumentException("--mode must be 'load' or 'observe'.");
        }

        var vehicles = ParseInt(values, "vehicles", 20, 1, 20);
        var durationSeconds = ParseInt(values, "duration-seconds", 900, 5, 24 * 60 * 60);
        var intervalMilliseconds = ParseInt(values, "interval-ms", 5000, 100, 60_000);
        var snapshotEverySeconds = ParseInt(values, "snapshot-every-seconds", 30, 5, 3600);
        var graceSeconds = ParseInt(values, "grace-seconds", 5, 0, 120);
        var runId = Get(values, "run-id", DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));
        var output = Get(values, "output", string.Empty);
        if (string.IsNullOrWhiteSpace(output))
        {
            output = Path.Combine(".runtime", "reliability", runId);
        }

        return new ReliabilityOptions
        {
            ApiBaseUrl = Get(values, "api-url", Environment.GetEnvironmentVariable("FLEETOPS_API_URL") ?? "http://localhost:5091").TrimEnd('/'),
            OrganizationSlug = Get(values, "slug", "northwind"),
            Vehicles = vehicles,
            DurationSeconds = durationSeconds,
            IntervalMilliseconds = intervalMilliseconds,
            SnapshotEverySeconds = snapshotEverySeconds,
            GraceSeconds = graceSeconds,
            InjectDuplicatesEvery = ParseInt(values, "inject-duplicates-every", 0, 0, 1000),
            InjectOutOfOrderEvery = ParseInt(values, "inject-out-of-order-every", 0, 0, 1000),
            SignalR = !values.ContainsKey("no-signalr"),
            Mode = mode,
            RunId = runId,
            OutputPath = output,
            OperatorEmail = Get(values, "operator-email", "operator@northwind.local"),
            OperatorPassword = Get(values, "operator-password", "Operator123!"),
        };
    }

    private static string Get(Dictionary<string, string> values, string key, string fallback) =>
        values.TryGetValue(key, out var value) ? value : fallback;

    private static int ParseInt(Dictionary<string, string> values, string key, int fallback, int minimum, int maximum)
    {
        if (!values.TryGetValue(key, out var raw))
        {
            return fallback;
        }

        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) || parsed < minimum || parsed > maximum)
        {
            throw new ArgumentException($"--{key} must be an integer between {minimum} and {maximum}.");
        }

        return parsed;
    }
}
