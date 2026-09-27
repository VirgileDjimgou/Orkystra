using System.Collections.Concurrent;

namespace FleetOps.Api.Demo;

public sealed record DemoSessionState(
    string Scenario,
    string Status,
    long Revision,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset ExpiresAtUtc);

public interface IDemoSessionStateStore
{
    DemoSessionState GetOrCreate(Guid sessionId, DateTimeOffset expiresAtUtc);
    DemoSessionState Apply(Guid sessionId, DateTimeOffset expiresAtUtc, string action, string? scenario);
    int RemoveExpired(DateTimeOffset now, int limit);
}

public sealed class DemoSessionStateStore(TimeProvider timeProvider) : IDemoSessionStateStore
{
    private static readonly HashSet<string> Scenarios = new(StringComparer.OrdinalIgnoreCase)
    {
        "NORMAL_SHIFT", "LATE_DELIVERY", "VEHICLE_ISSUE", "DRIVER_CONNECTIVITY_LOSS", "COMPLIANCE_WARNING"
    };

    private readonly ConcurrentDictionary<Guid, DemoSessionState> _states = new();

    public DemoSessionState GetOrCreate(Guid sessionId, DateTimeOffset expiresAtUtc) =>
        _states.GetOrAdd(sessionId, _ => NewState(expiresAtUtc));

    public DemoSessionState Apply(Guid sessionId, DateTimeOffset expiresAtUtc, string action, string? scenario)
    {
        var normalizedAction = action.Trim().ToUpperInvariant();
        if (normalizedAction is not ("START" or "PAUSE" or "RESET"))
        {
            throw new ArgumentException("Action must be START, PAUSE, or RESET.", nameof(action));
        }

        var normalizedScenario = string.IsNullOrWhiteSpace(scenario) ? "NORMAL_SHIFT" : scenario.Trim().ToUpperInvariant();
        if (!Scenarios.Contains(normalizedScenario))
        {
            throw new ArgumentException("Unknown Demo scenario.", nameof(scenario));
        }

        return _states.AddOrUpdate(
            sessionId,
            _ => Build(normalizedAction, normalizedScenario, 1, expiresAtUtc),
            (_, current) => Build(normalizedAction, normalizedScenario, current.Revision + 1, expiresAtUtc));
    }

    public int RemoveExpired(DateTimeOffset now, int limit)
    {
        var removed = 0;
        foreach (var entry in _states.Where(x => x.Value.ExpiresAtUtc <= now).Take(Math.Max(1, limit)))
        {
            if (_states.TryRemove(entry.Key, out _)) removed++;
        }

        return removed;
    }

    private DemoSessionState NewState(DateTimeOffset expiresAtUtc) =>
        new("NORMAL_SHIFT", "READY", 0, timeProvider.GetUtcNow(), expiresAtUtc);

    private DemoSessionState Build(string action, string scenario, long revision, DateTimeOffset expiresAtUtc) =>
        new(scenario, action == "START" ? "RUNNING" : action == "PAUSE" ? "PAUSED" : "READY", revision, timeProvider.GetUtcNow(), expiresAtUtc);
}
