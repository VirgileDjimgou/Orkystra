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

These are skeletons, not live integrations. Their job in this sprint is to prove that:

- the registry can swap providers by contract
- health and capability discovery are uniform
- canonical DTOs stay stable above the adapter boundary

## Current limitations

- no live vendor authentication
- no real network calls
- no connector persistence
- no event publication from adapters yet
- no provider configuration storage yet

## Connector Catalog Visibility

The first post-MVP connector visibility increment exposes a catalog endpoint for the current provider registry:

- providers can now be listed with identity, domain, kind, health, sync status, capabilities, schema, and supported read models
- the operator workspace can surface the catalog alongside the control tower overview
- the registry remains demo-backed, but its surface area is now explicit enough for future live provider configuration work
