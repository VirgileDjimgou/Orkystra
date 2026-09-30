# Tracking load baseline

Sprint 21 defines the operational baseline for the tenant-scoped current-position view. Sprint 31 adds measured budgets and an executable reliability harness. It remains a sizing guardrail, not a provider throughput claim.

| Fleet size | Telemetry cadence | UI update policy | Budget |
| --- | --- | --- | --- |
| 30 vehicles | one point / 5 seconds / vehicle | latest update per vehicle, coalesced for 250 ms | snapshot < 1 s; map remains interactive |
| 100 vehicles | one point / 5 seconds / vehicle | latest update per vehicle, coalesced for 250 ms | snapshot < 2 s; no queued update growth |

Sprint 31 measured budgets on the isolated development environment (see `RELIABILITY_REPORT.md`):

| Surface | Budget | Measured (2026-09-29) |
| --- | --- | --- |
| Ingestion (20 vehicles, 1 point/s) | p95 < 250 ms, 0 error | p95 45.9 ms, 0/4760 errors |
| Position snapshot (20 vehicles) | < 1 s | p95 29.9 ms |
| History catch-up (50 points) | < 1 s | p95 48.4 ms |
| SignalR delivery (client-observed) | p95 < 250 ms | p95 2.7 ms |

The hosted Demo engine and the internal scenario endpoint are bounded to **20 vehicles**. A 50-vehicle technical load therefore remains an explicit, untested limit for the hosted Demo; the 30/100 vehicle rows above remain design guardrails for a future environment exercise.

The server retains every accepted raw point for the configured retention period. The Worker deletes expired telemetry in bounded batches, outside the ingestion hot path, using the `IX_TelemetryPoints_RecordedAtUtc` index added in Sprint 31. It derives quality, trips, and zone events without overwriting raw telemetry. The Web client coalesces the latest pending SignalR update **per vehicle** during each 250 ms interval: an update for one vehicle never replaces another vehicle's pending update. After reconnect it reloads the tenant-scoped snapshot before marking visual state current.

Run the deterministic development trace before a release:

```powershell
dotnet test tests/backend/FleetOps.UnitTests/FleetOps.UnitTests.csproj --filter "FullyQualifiedName~TrackingIntegrationTests"
```

Run the measured reliability exercise before a release:

```powershell
pwsh -ExecutionPolicy Bypass -File scripts/run-reliability-exercise.ps1 -Mode load -DurationSeconds 60 -Vehicles 20 -IntervalMs 1000 -SkipWorker
```

For an environment load exercise, seed only synthetic assets in an isolated tenant, publish at the stated cadence, record p50/p95 snapshot latency and browser responsiveness, then delete that tenant. Do not use production personal or location data for this baseline.
