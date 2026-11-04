# Sprint 34 — measured simulation evidence

Synthetic development evidence, captured 2026-11-04 on Windows, .NET 10.0.12, 16 logical processors. No production throughput or real-road routing claim.

## Before and after

The preserved observer report `.runtime/reliability/20260929-222644/reliability-report.md` recorded 20 vehicles over 900.3 seconds, 308 quality rejections, zero HTTP errors and zero missing positions. The old open Neckar fixture had a 4.225454 km endpoint-to-start gap. Its movement advanced by tick count while reported speed was independent of distance.

The new observer run `20261104-013902`, `.runtime/sprint34-reliability/reliability-report.md`, ran 20 vehicles for 90.8 seconds with a real Worker process restart injected at 30 seconds. Results: 1,748 events observed since baseline, 1,760 total accepted, zero quality rejections, zero out-of-order events, zero HTTP errors, 20 current positions, 1,758 SignalR messages. Snapshot p50/p95: 2.33/4.62 ms; history p50/p95: 2.79/23.67 ms. Observer mode does not measure per-request ingestion or broadcast latency. Durations and cadence differ from September: these are regression observations, not a comparative load benchmark.

Reproduce in an isolated synthetic environment:

```powershell
pwsh -File scripts/run-reliability-exercise.ps1 -Mode observe -DurationSeconds 90 -Vehicles 20 -TickSeconds 1 -WorkerRestartAfterSeconds 30 -SnapshotEverySeconds 10
```

Observer success now additionally requires no increase in quality rejections or out-of-order events.

A final run `20261104-014456` on the final seeded-speed implementation and the strengthened observer gate passed for 60.6 seconds with twenty vehicles and a Worker restart at 20 seconds. It received 1,140 SignalR messages, with zero quality rejections, zero HTTP errors and twenty current positions. Full counters are preserved under `.runtime/sprint34-final-reliability/`.

## Captured wrap trace

Captured from compiled `DemoRouteMotion.Sample` for Neckar, target 42 km/h. Seconds are logical elapsed time; the whole cycle is 1385.339869 seconds. Positions are synthetic. The next cycle begins at exactly the same depot coordinate, with zero speed and stable incoming heading during dwell.

```csv
seconds,latitude,longitude,speedKph,heading,anchor,dwell
1383.339869,48.775801669,9.182906271,1.783735,248.009662,7,False
1384.339869,48.775800417,9.182901568,0.892069,248.009662,7,False
1385.339869,48.775800000,9.182900000,0.000000,248.009662,0,True
1386.339869,48.775800000,9.182900000,0.000000,248.009662,0,True
1387.339869,48.775800000,9.182900000,0.000000,248.009662,0,True
1405.339869,48.775800000,9.182900000,0.000000,67.999207,0,False
1410.339869,48.775810424,9.182939161,4.452298,67.999207,0,False
```

## Regression coverage

- `DemoMotionTests`: every second of 24 logical hours, all four routes and five scenarios; bounded displacement, speed, acceleration and stable dwell heading; all anchors visited. Variable cadence and restored snapshots yield identical events for twenty vehicles; v1 restore is rejected explicitly.
- `DemoScenarioIntegrationTests.TwentyVehiclesRemainReliableThroughThreeHoursAndSnapshotRestart`: 3,600 canonical API events, twenty vehicles, three logical hours, restore at midpoint; exactly 3,600 accepted/history rows, zero rejections/out-of-order, twenty final positions with quality 100 and sequence 180.
- `DemoScenarioRestartReliabilityTests`: persisted file round-trip, twenty vehicles, exact replay equality and bounded first displacement after restart.
- `ObservabilityReliabilityTests`: actual hosted loop plus legacy-snapshot migration (reset before clear/start), metrics retained.
- `VirtualDriverWorkflowIntegrationTests`: real inspection/start; arrival waits without telemetry; canonical position at the declared stop permits arrival, proof and completion.
- Existing isolation, SignalR reconnect/catch-up, SQL persistence, reset and history suites remain required by the full gate.

## Local infrastructure repair

MinIO's legacy image runs as UID 1001. The existing bind directory contained root-owned `.minio.sys` files and failed on rename. A one-off scoped `chown -R 1001:1001 /bitnami/minio/data` inside the stopped MinIO service's mounted directory restored access. No files were removed; subsequent gate provisioning and the real MinIO contract test passed. Production permissions and authorization were not changed.

The complete validation journal is `.runtime/sprint34-quality-gate.log`. The machine state and quality report record the final gate result after completion.

The first complete attempt reached the Web dependency audit and found four newly reported High advisories. Compatible patch updates, including Vue 3.5.44 and Vite 7.3.7, removed all High findings. Sixteen Moderate findings remain in development tooling; the existing gate threshold is High, unchanged. No forced major downgrade or audit suppression was used. The earlier failed audit is retained in `.runtime/sprint34-quality-gate-before-dependency-fix.log`.
