# Tracking load baseline

Sprint 21 defines the operational baseline for the tenant-scoped current-position view. It is a sizing guardrail, not a provider throughput claim.

| Fleet size | Telemetry cadence | UI update policy | Budget |
| --- | --- | --- | --- |
| 30 vehicles | one point / 5 seconds / vehicle | latest update per vehicle, coalesced for 250 ms | snapshot < 1 s; map remains interactive |
| 100 vehicles | one point / 5 seconds / vehicle | latest update per vehicle, coalesced for 250 ms | snapshot < 2 s; no queued update growth |

The server retains every accepted raw point for the configured retention period. It derives quality, trips, and zone events without overwriting raw telemetry. The Web client renders only the most recent pending SignalR update during each 250 ms interval; after reconnect it reloads the tenant-scoped snapshot before accepting further visual state as current.

Run the deterministic development trace before a release:

```powershell
dotnet test tests/backend/FleetOps.UnitTests/FleetOps.UnitTests.csproj --filter "FullyQualifiedName~TrackingIntegrationTests"
```

For an environment load exercise, seed only synthetic assets in an isolated tenant, publish at the stated cadence, record p50/p95 snapshot latency and browser responsiveness, then delete that tenant. Do not use production personal or location data for this baseline.
