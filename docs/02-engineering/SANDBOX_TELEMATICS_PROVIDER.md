# Sandbox Telematics Provider

The internal provider is a virtual, non-commercial HTTP adapter for demonstration and contract testing. Its contract version is `sandbox-telematics.v1`; it is deliberately isolated from FleetOps tracking through a canonical event.

Send `POST /api/v1/integrations/device/sandbox-telematics/events` with a Device API key and this schema:

```json
{"contractVersion":"sandbox-telematics.v1","vehicleId":"<tenant vehicle>","providerEventId":"evt-001","deviceExternalId":"NW-GPS-100","occurredAtUtc":"2026-07-22T12:00:00Z","latitude":48.49,"longitude":9.20,"speedKph":32,"headingDegrees":90,"sequenceNumber":1,"accuracyMeters":8,"odometerKm":1200,"eventKind":"position"}
```

The provider event ID is namespaced before it reaches tracking, so a replay is safely handled by the existing canonical ingestion deduplication. Replace this adapter, not the tracking domain, when a real provider is selected. Rotate the Device API key by creating a replacement, switching the simulator to it, then revoking the prior key; never place the credential in source control or logs.
