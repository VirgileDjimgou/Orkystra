using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.SignalR.Client;

namespace FleetOpsReliabilityHarness;

public sealed class ReliabilityRunner(ReliabilityOptions options) : IDisposable
{
    private readonly HttpClient _http = new() { BaseAddress = new Uri(options.ApiBaseUrl), Timeout = TimeSpan.FromSeconds(30) };
    private readonly LatencyRecorder _ingestionLatency = new();
    private readonly LatencyRecorder _snapshotLatency = new();
    private readonly LatencyRecorder _historyLatency = new();
    private readonly LatencyRecorder _signalRLatency = new();
    private readonly ConcurrentDictionary<(Guid VehicleId, long Sequence), long> _sentTicks = new();
    private readonly ConcurrentDictionary<Guid, long> _signalRSequences = new();
    private long _eventsSent;
    private long _httpErrors;
    private long _duplicateInjections;
    private long _outOfOrderInjections;
    private long _signalRMessages;

    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(options.OutputPath);
        var startedAtUtc = DateTimeOffset.UtcNow;
        Console.WriteLine($"FleetOps reliability harness [{options.Mode}] run {options.RunId}");
        Console.WriteLine($"API={options.ApiBaseUrl} slug={options.OrganizationSlug} vehicles={options.Vehicles} duration={options.DurationSeconds}s interval={options.IntervalMilliseconds}ms");

        var scenario = await LoadScenarioAsync(cancellationToken);
        var token = await LoginAsync(cancellationToken);

        HubConnection? connection = null;
        if (options.SignalR)
        {
            connection = BuildConnection(token);
            await connection.StartAsync(cancellationToken);
            Console.WriteLine("SignalR tracking stream connected.");
        }

        try
        {
            return options.Mode == "observe"
                ? await ObserveAsync(scenario, token, startedAtUtc, cancellationToken)
                : await LoadAsync(scenario, token, startedAtUtc, cancellationToken);
        }
        finally
        {
            if (connection is not null)
            {
                await connection.DisposeAsync();
            }
        }
    }

    private async Task<int> LoadAsync(ScenarioResponse scenario, string token, DateTimeOffset startedAtUtc, CancellationToken cancellationToken)
    {
        var startUtc = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        var nextSnapshotAt = startUtc.AddSeconds(options.SnapshotEverySeconds);
        var nextTickAt = DateTimeOffset.UtcNow;
        long tick = 0;
        IngestTelemetryRequest? previous = null;

        while (stopwatch.Elapsed < TimeSpan.FromSeconds(options.DurationSeconds) && !cancellationToken.IsCancellationRequested)
        {
            tick++;
            var recordedAtUtc = startUtc.AddMilliseconds(tick * options.IntervalMilliseconds);
            var batch = scenario.Vehicles
                .Select((vehicle, index) => BuildRequest(scenario, vehicle, index, tick, recordedAtUtc))
                .ToList();

            await Task.WhenAll(batch.Select(request => SendAsync(request, cancellationToken)));
            previous = batch[0];

            if (options.InjectDuplicatesEvery > 0 && tick % options.InjectDuplicatesEvery == 0 && previous is not null)
            {
                _duplicateInjections++;
                await SendAsync(previous, cancellationToken);
            }

            if (options.InjectOutOfOrderEvery > 0 && tick % options.InjectOutOfOrderEvery == 0)
            {
                _outOfOrderInjections++;
                await SendAsync(
                    batch[^1] with
                    {
                        EventId = batch[^1].EventId + "-older",
                        RecordedAtUtc = recordedAtUtc.AddMinutes(-5),
                    },
                    cancellationToken);
            }

            if (DateTimeOffset.UtcNow >= nextSnapshotAt)
            {
                await SampleSnapshotAsync(scenario, token, cancellationToken);
                nextSnapshotAt = nextSnapshotAt.AddSeconds(options.SnapshotEverySeconds);
            }

            nextTickAt = nextTickAt.AddMilliseconds(options.IntervalMilliseconds);
            var delay = nextTickAt - DateTimeOffset.UtcNow;
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken);
            }
        }

        stopwatch.Stop();
        if (options.GraceSeconds > 0)
        {
            await Task.Delay(TimeSpan.FromSeconds(options.GraceSeconds), cancellationToken);
        }

        var positions = await FetchPositionsAsync(token, cancellationToken);
        var metrics = await FetchMetricsAsync(token, cancellationToken);
        var missing = scenario.Vehicles.Count(vehicle => positions.All(position => position.VehicleId != vehicle.VehicleId));
        var expectedAccepted = _eventsSent - _duplicateInjections;
        var passed = missing == 0
            && metrics.AcceptedCount >= expectedAccepted
            && metrics.DuplicateCount >= _duplicateInjections
            && metrics.OutOfOrderCount >= _outOfOrderInjections
            && _httpErrors == 0;

        var endedAtUtc = DateTimeOffset.UtcNow;
        var report = new ReliabilityReport(
            options.RunId,
            options.Mode,
            options.ApiBaseUrl,
            options.OrganizationSlug,
            scenario.Vehicles.Count,
            options.DurationSeconds,
            options.IntervalMilliseconds,
            startedAtUtc,
            endedAtUtc,
            stopwatch.Elapsed.TotalSeconds,
            _eventsSent,
            metrics.AcceptedCount,
            _httpErrors,
            _eventsSent == 0 ? 0 : (double)_httpErrors / _eventsSent,
            _duplicateInjections,
            _outOfOrderInjections,
            metrics.DuplicateCount,
            metrics.OutOfOrderCount,
            metrics.RejectedCount,
            _ingestionLatency.Summary(),
            _snapshotLatency.Summary(),
            _historyLatency.Summary(),
            _signalRLatency.Summary(),
            _signalRMessages,
            missing,
            scenario.Vehicles.Count - missing,
            DescribeEnvironment(),
            "Synthetic development tenants only; in-process or single-instance API; internal ingestion endpoints are Development-only.",
            passed);

        await ReliabilityReportWriter.WriteAsync(report, options.OutputPath, cancellationToken);
        Console.WriteLine(ReliabilityReportWriter.Summarize(report));
        return passed ? 0 : 2;
    }

    private async Task<int> ObserveAsync(ScenarioResponse scenario, string token, DateTimeOffset startedAtUtc, CancellationToken cancellationToken)
    {
        var baseline = await FetchPositionsAsync(token, cancellationToken);
        var metricsBefore = await FetchMetricsAsync(token, cancellationToken);
        var stopwatch = Stopwatch.StartNew();
        var nextSnapshotAt = DateTimeOffset.UtcNow.AddSeconds(options.SnapshotEverySeconds);

        while (stopwatch.Elapsed < TimeSpan.FromSeconds(options.DurationSeconds) && !cancellationToken.IsCancellationRequested)
        {
            if (DateTimeOffset.UtcNow >= nextSnapshotAt)
            {
                await SampleSnapshotAsync(scenario, token, cancellationToken);
                nextSnapshotAt = nextSnapshotAt.AddSeconds(options.SnapshotEverySeconds);
            }

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }

        stopwatch.Stop();
        if (options.GraceSeconds > 0)
        {
            await Task.Delay(TimeSpan.FromSeconds(options.GraceSeconds), cancellationToken);
        }

        var positions = await FetchPositionsAsync(token, cancellationToken);
        var metricsAfter = await FetchMetricsAsync(token, cancellationToken);
        var baselineByVehicle = baseline.ToDictionary(position => position.VehicleId, position => position.SequenceNumber ?? 0);
        var missing = 0;
        var advanced = 0;
        foreach (var vehicle in scenario.Vehicles)
        {
            var current = positions.FirstOrDefault(position => position.VehicleId == vehicle.VehicleId);
            if (current is null)
            {
                missing++;
                continue;
            }

            baselineByVehicle.TryGetValue(vehicle.VehicleId, out var baselineSequence);
            if ((current.SequenceNumber ?? 0L) > baselineSequence)
            {
                advanced++;
            }
        }

        var signalRMessages = Interlocked.Read(ref _signalRMessages);
        var signalRCoverage = _signalRSequences.Count;
        var passed = missing == 0
            && advanced == scenario.Vehicles.Count
            && metricsAfter.AcceptedCount > metricsBefore.AcceptedCount
            && _httpErrors == 0
            && (!options.SignalR || (signalRMessages > 0 && signalRCoverage == scenario.Vehicles.Count));

        var report = new ReliabilityReport(
            options.RunId,
            options.Mode,
            options.ApiBaseUrl,
            options.OrganizationSlug,
            scenario.Vehicles.Count,
            options.DurationSeconds,
            options.IntervalMilliseconds,
            startedAtUtc,
            DateTimeOffset.UtcNow,
            stopwatch.Elapsed.TotalSeconds,
            metricsAfter.AcceptedCount - metricsBefore.AcceptedCount,
            metricsAfter.AcceptedCount,
            _httpErrors,
            0,
            0,
            0,
            metricsAfter.DuplicateCount,
            metricsAfter.OutOfOrderCount,
            metricsAfter.RejectedCount,
            _ingestionLatency.Summary(),
            _snapshotLatency.Summary(),
            _historyLatency.Summary(),
            _signalRLatency.Summary(),
            signalRMessages,
            missing,
            advanced,
            DescribeEnvironment(),
            "Observer mode measures the externally hosted Demo engine; ingestion latency is not sampled per request.",
            passed);

        await ReliabilityReportWriter.WriteAsync(report, options.OutputPath, cancellationToken);
        Console.WriteLine(ReliabilityReportWriter.Summarize(report));
        return passed ? 0 : 2;
    }

    private async Task SendAsync(IngestTelemetryRequest request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _eventsSent);
        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var response = await _http.PostAsJsonAsync("/api/internal/v1/tracking/events", request, cancellationToken);
            stopwatch.Stop();
            if (!response.IsSuccessStatusCode)
            {
                Interlocked.Increment(ref _httpErrors);
                Console.Error.WriteLine($"Ingestion failed: {(int)response.StatusCode} for {request.EventId}");
                return;
            }

            _ingestionLatency.Add(stopwatch.Elapsed.TotalMilliseconds);
            _sentTicks[(request.VehicleId, request.SequenceNumber)] = Stopwatch.GetTimestamp();
        }
        catch (HttpRequestException exception)
        {
            Interlocked.Increment(ref _httpErrors);
            Console.Error.WriteLine($"Ingestion transport error: {exception.Message}");
        }
    }

    private async Task SampleSnapshotAsync(ScenarioResponse scenario, string token, CancellationToken cancellationToken)
    {
        var positionsWatch = Stopwatch.StartNew();
        await FetchPositionsAsync(token, cancellationToken);
        positionsWatch.Stop();
        _snapshotLatency.Add(positionsWatch.Elapsed.TotalMilliseconds);

        var historyWatch = Stopwatch.StartNew();
        using var response = await _http.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, $"/api/v1/tracking/history?vehicleId={scenario.Vehicles[0].VehicleId}&page=1&pageSize=50")
            {
                Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) },
            },
            cancellationToken);
        historyWatch.Stop();
        if (!response.IsSuccessStatusCode)
        {
            Interlocked.Increment(ref _httpErrors);
            Console.Error.WriteLine($"History snapshot failed: {(int)response.StatusCode}");
            return;
        }

        _historyLatency.Add(historyWatch.Elapsed.TotalMilliseconds);
    }

    private async Task<ScenarioResponse> LoadScenarioAsync(CancellationToken cancellationToken)
    {
        var scenario = await _http.GetFromJsonAsync<ScenarioResponse>(
            $"/api/internal/v1/tracking/scenarios/{options.OrganizationSlug}?maxVehicles={options.Vehicles}",
            cancellationToken);
        if (scenario is null)
        {
            throw new InvalidOperationException("The tracking scenario endpoint returned no payload. The API must run in Development.");
        }

        if (scenario.Vehicles.Count != options.Vehicles)
        {
            throw new InvalidOperationException(
                $"Requested {options.Vehicles} vehicles but the scenario exposes {scenario.Vehicles.Count}. Extend the synthetic seed or lower --vehicles.");
        }

        return scenario;
    }

    private async Task<string> LoginAsync(CancellationToken cancellationToken)
    {
        using var response = await _http.PostAsJsonAsync(
            "/api/auth/login",
            new { email = options.OperatorEmail, password = options.OperatorPassword },
            cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Operator login failed with {(int)response.StatusCode}.");
        }

        var login = await response.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken);
        if (string.IsNullOrWhiteSpace(login?.AccessToken))
        {
            throw new InvalidOperationException("Operator login returned no access token.");
        }

        return login!.AccessToken;
    }

    private HubConnection BuildConnection(string token) =>
        new HubConnectionBuilder()
            .WithUrl($"{options.ApiBaseUrl}/hubs/tracking", connectionOptions =>
            {
                connectionOptions.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .WithAutomaticReconnect()
            .Build()
            .Tap(connection => connection.On<PositionResponse>("trackingPositionChanged", position =>
            {
                Interlocked.Increment(ref _signalRMessages);
                if (position.SequenceNumber.HasValue)
                {
                    _signalRSequences[position.VehicleId] = position.SequenceNumber.Value;
                    if (_sentTicks.TryRemove((position.VehicleId, position.SequenceNumber.Value), out var sentTicks))
                    {
                        var elapsed = Stopwatch.GetElapsedTime(sentTicks);
                        _signalRLatency.Add(elapsed.TotalMilliseconds);
                    }
                }
            }));

    private async Task<List<PositionResponse>> FetchPositionsAsync(string token, CancellationToken cancellationToken)
    {
        using var response = await _http.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, "/api/v1/tracking/positions")
            {
                Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) },
            },
            cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            Interlocked.Increment(ref _httpErrors);
            throw new InvalidOperationException($"Positions snapshot failed with {(int)response.StatusCode}.");
        }

        return (await response.Content.ReadFromJsonAsync<List<PositionResponse>>(cancellationToken)) ?? [];
    }

    private async Task<MetricsResponse> FetchMetricsAsync(string token, CancellationToken cancellationToken)
    {
        using var response = await _http.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, "/api/v1/tracking/metrics")
            {
                Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) },
            },
            cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            Interlocked.Increment(ref _httpErrors);
            throw new InvalidOperationException($"Metrics snapshot failed with {(int)response.StatusCode}.");
        }

        return (await response.Content.ReadFromJsonAsync<MetricsResponse>(cancellationToken))
            ?? throw new InvalidOperationException("Metrics endpoint returned no payload.");
    }

    private static IngestTelemetryRequest BuildRequest(
        ScenarioResponse scenario,
        ScenarioVehicle vehicle,
        int index,
        long tick,
        DateTimeOffset recordedAtUtc)
    {
        var latitude = 48.7758 + index * 0.004 + Math.Sin(tick / 20d) * 0.002;
        var longitude = 9.1829 + index * 0.004 + Math.Cos(tick / 20d) * 0.002;
        return new IngestTelemetryRequest(
            scenario.OrganizationId,
            vehicle.VehicleId,
            vehicle.DeviceId,
            $"reliability-{tick:D6}-{index:D2}",
            recordedAtUtc,
            latitude,
            longitude,
            35 + index % 10,
            (index * 37 + tick * 11) % 360,
            tick,
            5,
            "reliability-harness");
    }

    public void Dispose() => _http.Dispose();

    private static string DescribeEnvironment() =>
        JsonSerializer.Serialize(new
        {
            machine = Environment.MachineName,
            os = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
            processors = Environment.ProcessorCount,
            runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
        });
}

internal static class ConnectionExtensions
{
    public static HubConnection Tap(this HubConnection connection, Action<HubConnection> configure)
    {
        configure(connection);
        return connection;
    }
}

internal sealed record ScenarioResponse(
    Guid OrganizationId,
    string OrganizationName,
    string OrganizationSlug,
    IReadOnlyList<ScenarioVehicle> Vehicles);

internal sealed record ScenarioVehicle(
    Guid VehicleId,
    string RegistrationNumber,
    string DisplayName,
    string DeviceId);

internal sealed record LoginResponse(string AccessToken);

internal sealed record PositionResponse(
    [property: JsonPropertyName("vehicleId")] Guid VehicleId,
    [property: JsonPropertyName("registrationNumber")] string RegistrationNumber,
    [property: JsonPropertyName("sequenceNumber")] long? SequenceNumber,
    [property: JsonPropertyName("recordedAtUtc")] DateTimeOffset RecordedAtUtc,
    [property: JsonPropertyName("qualityStatus")] string QualityStatus);

internal sealed record MetricsResponse(
    int CurrentVehicleCount,
    int HistoryPointCount,
    long AcceptedCount,
    long DuplicateCount,
    long OutOfOrderCount,
    int RetentionDays,
    long RejectedCount = 0);

internal sealed record IngestTelemetryRequest(
    Guid OrganizationId,
    Guid VehicleId,
    string DeviceId,
    string EventId,
    DateTimeOffset RecordedAtUtc,
    double Latitude,
    double Longitude,
    double SpeedKph,
    double HeadingDegrees,
    long SequenceNumber,
    double AccuracyMeters,
    string Source);

internal sealed record LatencySummary(int Samples, double P50, double P95, double Max, double Mean);

internal sealed class LatencyRecorder
{
    private readonly List<double> _values = [];
    private readonly object _gate = new();

    public void Add(double milliseconds)
    {
        lock (_gate)
        {
            _values.Add(milliseconds);
        }
    }

    public LatencySummary Summary()
    {
        lock (_gate)
        {
            if (_values.Count == 0)
            {
                return new LatencySummary(0, 0, 0, 0, 0);
            }

            var sorted = _values.OrderBy(value => value).ToList();
            return new LatencySummary(
                sorted.Count,
                Percentile(sorted, 0.50),
                Percentile(sorted, 0.95),
                sorted[^1],
                sorted.Average());
        }
    }

    private static double Percentile(List<double> sorted, double percentile)
    {
        var index = (int)Math.Ceiling(percentile * sorted.Count) - 1;
        return sorted[Math.Clamp(index, 0, sorted.Count - 1)];
    }
}

internal sealed record ReliabilityReport(
    string RunId,
    string Mode,
    string ApiBaseUrl,
    string OrganizationSlug,
    int Vehicles,
    int DurationSeconds,
    int IntervalMilliseconds,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset EndedAtUtc,
    double ElapsedSeconds,
    long EventsSent,
    long AcceptedCount,
    long HttpErrors,
    double ErrorRate,
    long DuplicateInjections,
    long OutOfOrderInjections,
    long DuplicateCount,
    long OutOfOrderCount,
    long RejectedCount,
    LatencySummary IngestionLatencyMs,
    LatencySummary SnapshotLatencyMs,
    LatencySummary HistoryLatencyMs,
    LatencySummary SignalRLatencyMs,
    long SignalRMessages,
    int MissingCurrentPositions,
    int VehiclesWithCurrentPositions,
    string Environment,
    string Limitations,
    bool Passed);

internal static class ReliabilityReportWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static async Task WriteAsync(ReliabilityReport report, string outputPath, CancellationToken cancellationToken)
    {
        var jsonPath = Path.Combine(outputPath, "reliability-report.json");
        await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(report, JsonOptions), cancellationToken);
        var markdownPath = Path.Combine(outputPath, "reliability-report.md");
        await File.WriteAllTextAsync(markdownPath, BuildMarkdown(report), cancellationToken);
        Console.WriteLine($"Report written to {markdownPath}");
    }

    public static string Summarize(ReliabilityReport report) =>
        $"""
        mode={report.Mode} passed={report.Passed} vehicles={report.Vehicles} elapsed={report.ElapsedSeconds:F0}s
        ingestion p50={report.IngestionLatencyMs.P50:F1}ms p95={report.IngestionLatencyMs.P95:F1}ms max={report.IngestionLatencyMs.Max:F1}ms samples={report.IngestionLatencyMs.Samples}
        snapshot p50={report.SnapshotLatencyMs.P50:F1}ms p95={report.SnapshotLatencyMs.P95:F1}ms | signalR p50={report.SignalRLatencyMs.P50:F1}ms p95={report.SignalRLatencyMs.P95:F1}ms messages={report.SignalRMessages}
        httpErrors={report.HttpErrors} missingPositions={report.MissingCurrentPositions} vehiclesWithPositions={report.VehiclesWithCurrentPositions}
        """;

    private static string BuildMarkdown(ReliabilityReport report) =>
        $"""
        # FleetOps reliability harness report

        **SIMULATED DEVELOPMENT EVIDENCE — NOT PILOT OR COMMERCIAL PROOF**

        | Field | Value |
        | --- | --- |
        | Run id | `{report.RunId}` |
        | Mode | {report.Mode} |
        | API | {report.ApiBaseUrl} |
        | Organization | {report.OrganizationSlug} |
        | Vehicles | {report.Vehicles} |
        | Configured duration | {report.DurationSeconds} s |
        | Interval | {report.IntervalMilliseconds} ms |
        | Started | {report.StartedAtUtc:O} |
        | Ended | {report.EndedAtUtc:O} |
        | Elapsed | {report.ElapsedSeconds:F1} s |
        | Passed | {report.Passed} |

        ## Measured latencies

        | Surface | Samples | p50 (ms) | p95 (ms) | max (ms) | mean (ms) |
        | --- | --- | --- | --- | --- | --- |
        | Ingestion | {report.IngestionLatencyMs.Samples} | {report.IngestionLatencyMs.P50:F2} | {report.IngestionLatencyMs.P95:F2} | {report.IngestionLatencyMs.Max:F2} | {report.IngestionLatencyMs.Mean:F2} |
        | Position snapshot | {report.SnapshotLatencyMs.Samples} | {report.SnapshotLatencyMs.P50:F2} | {report.SnapshotLatencyMs.P95:F2} | {report.SnapshotLatencyMs.Max:F2} | {report.SnapshotLatencyMs.Mean:F2} |
        | History catch-up | {report.HistoryLatencyMs.Samples} | {report.HistoryLatencyMs.P50:F2} | {report.HistoryLatencyMs.P95:F2} | {report.HistoryLatencyMs.Max:F2} | {report.HistoryLatencyMs.Mean:F2} |
        | SignalR broadcast | {report.SignalRLatencyMs.Samples} | {report.SignalRLatencyMs.P50:F2} | {report.SignalRLatencyMs.P95:F2} | {report.SignalRLatencyMs.Max:F2} | {report.SignalRLatencyMs.Mean:F2} |

        ## Counters

        - events sent / accepted: {report.EventsSent} / {report.AcceptedCount}
        - duplicate injections / duplicates observed: {report.DuplicateInjections} / {report.DuplicateCount}
        - out-of-order injections / out-of-order observed: {report.OutOfOrderInjections} / {report.OutOfOrderCount}
        - quality-rejected points: {report.RejectedCount}
        - HTTP errors / error rate: {report.HttpErrors} / {report.ErrorRate:P3}
        - SignalR messages: {report.SignalRMessages}
        - missing current positions: {report.MissingCurrentPositions}
        - vehicles with current positions: {report.VehiclesWithCurrentPositions}

        ## Environment

        ```json
        {report.Environment}
        ```

        ## Limitations

        {report.Limitations}
        """;
}
