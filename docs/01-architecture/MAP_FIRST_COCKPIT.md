# Map-First Operations Cockpit

The authenticated `/` route becomes the primary operational cockpit. It composes existing tracking, dispatch, and operations APIs rather than creating a parallel operational model. `/map` remains a compatible route with `vehicleId` and `missionRef` focus support.

```text
Cockpit toolbar
├── Fleet map canvas (dominant desktop surface)
│   ├── vehicle markers: identity, heading, state, freshness, exception
│   └── selected trail or mission route only
├── Context inspector: vehicle, driver, mission, or exception
└── Activity dock: Exceptions | Missions | Agent activity | Timeline
```

Markers use non-colour cues and progressive disclosure. A selection in any surface focuses the related vehicle or location and loads only the context needed by the inspector. The map does not render all historical trails at once.

Leaflet remains the adapter. `MapTileProviderConfiguration` supplies the tile URL and attribution from runtime/environment configuration; no cockpit component hard-codes a hosted provider.

Frontend boundaries belong under feature-level components/composables. Page views coordinate state and should not grow into multi-hundred-line containers.
