# Connector Layer

Sprint 9 introduces the first provider-facing connector layer in the backend application.

## Design principles

- Providers expose the same contract surface whether they are simulators or real connectors.
- Vendor schemas stay outside the core domain and are mapped into canonical contracts.
- Application services resolve providers through a registry, not through concrete adapter references.
- Health, capabilities, sync status, schema description, and error surface are first-class integration concerns.

## Current provider contract

All providers now expose:

- provider identity
- domain
- provider kind
- health report
- capability set
- sync status
- schema description

Domain-specific adapters additionally expose canonical reads:

- warehouse providers return `WarehouseSummaryReadModel`
- transport providers return `RouteSummaryReadModel`
- GPS providers return `GpsPositionSnapshot`

## Current skeleton adapters

- `CsvWarehouseImportProvider`
- `RestTransportProvider`
- `GpsTelematicsProvider`

These started as skeletons, but the transport slice has now crossed into a first disciplined live-read workflow:

- the registry can swap providers by contract
- health and capability discovery are uniform
- canonical DTOs stay stable above the adapter boundary
- the REST transport adapter can read a live upstream when runtime configuration is valid

## Connector Catalog Visibility

The first post-MVP connector visibility increment exposes a catalog endpoint for the current provider registry:

- providers can now be listed with identity, domain, kind, health, sync status, capabilities, schema, and supported read models
- the operator workspace can surface the catalog alongside the control tower overview
- the registry remains demo-backed, but its surface area is now explicit enough for future live provider configuration work

## Editable Provider Configuration

Sprint 17 adds a local configuration-editing workflow for non-secret provider settings:

- the provider catalog now exposes editable safe fields alongside readiness and missing-field posture
- operators can update enabled state, environment, and approved runtime fields from the UI
- updates are persisted to `backend/src/Orkystra.Api/appsettings.Local.json`, which stays local and ignored by Git
- the API only accepts known providers and approved non-secret fields, so secret material is still kept out of the operator surface

## Current limitations

- no connector writeback flow yet
- no centralized remote connector persistence or fleet-wide secret manager yet
- only the GPS provider currently feeds connector-originated telemetry through MQTT; the transport adapter still imports snapshots through pull sync
- no durable inbox/outbox persistence for the event backbone yet

## MQTT-backed connector telemetry

The connector layer now has its first brokered telemetry path:

- the GPS provider returns canonical `GpsPositionSnapshot` payloads
- the API publishes those payloads to the configured GPS stream topic through the shared event-envelope model
- the MQTT consumer dispatches those telemetry events through the projection runner
- operators can read the latest projected truck positions through `GET /api/gps/positions`

This keeps connector-originated events aligned with the same routing and projection infrastructure used by simulation traffic.

## Transport live snapshot import

Sprint 29 adds the first explicit transport import workflow on top of the live REST adapter:

- `POST /api/transport/sync` imports the current transport route snapshot through the provider registry
- imported route summaries and route details are persisted into the operational store and become reusable by later API reads
- `GET /api/transport/sync-status` exposes the latest sync evidence, including route count, imported references, live-vs-fallback source, and provider health
- the transport slice now has a clear distinction between "current provider posture" and "last imported operational snapshot"

This is still a narrow first synchronization workflow rather than a full CDC or webhook integration, but it creates a durable handoff between live provider reads and operator-facing projections.
