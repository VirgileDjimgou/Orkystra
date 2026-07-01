export function mockControlTowerOverview() {
  return {
    tenantId: 'e2e-test-tenant',
    generatedAtUtc: new Date().toISOString(),
    scenarios: [
      { scenarioId: 's-001', name: 'E2E Test Scenario', status: 'Running', seed: 42, currentTime: new Date().toISOString(), injectedEventCount: 3 },
    ],
    warehouses: [
      { warehouseId: 'w-001', name: 'E2E North Hub', zoneCount: 3, rackCount: 12, slotCount: 480, occupiedDockCount: 2, storedPalletCount: 215 },
    ],
    routes: [
      { routeId: 'r-001', reference: 'E2E-RT-1', truckId: 't-001', truckReference: 'TRUCK-01', status: 'On time', stopCount: 4, shipmentCount: 3, completedDeliveryCount: 1, nextEtaLabel: '14:30', mapX: '52.52', mapY: '13.40' },
    ],
    alerts: [],
    eventFeed: [{ eventId: 'evt-1', timeLabel: 'now', title: 'System ready', description: 'E2E test environment loaded' }],
    providers: [{ providerId: 'e2e-provider', providerName: 'E2E Provider', domain: 'transport', healthStatus: 'Healthy', syncStatus: 'idle', lastSuccessfulSyncAt: new Date().toISOString(), lastAttemptedSyncAt: new Date().toISOString(), summary: 'E2E provider ready' }],
  };
}

export function mockTransportSyncStatus() {
  return {
    providerId: 'e2e-provider',
    source: 'live',
    liveImport: true,
    hasPersistedSnapshot: true,
    importedRouteCount: 3,
    importedRouteIds: ['r-001', 'r-002', 'r-003'],
    importedRouteReferences: ['E2E-RT-1', 'E2E-RT-2', 'E2E-RT-3'],
    lastImportedAtUtc: new Date().toISOString(),
    lastSuccessfulSyncAt: new Date().toISOString(),
    lastAttemptedSyncAt: new Date().toISOString(),
    syncStatus: 'healthy',
    syncDetail: 'Import completed successfully',
    health: { providerId: 'e2e-provider', providerName: 'E2E Provider', status: 'Healthy', checkedAt: new Date().toISOString(), summary: 'All systems operational', signals: ['connected'] },
  };
}

export function mockProviderCatalog() {
  return {
    generatedAtUtc: new Date().toISOString(),
    providers: [
      {
        providerId: 'rest-transport-adapter',
        providerName: 'REST Transport Adapter',
        domain: 'transport',
        kind: 'rest',
        configuration: {
          enabled: true, environment: 'demo', readiness: 'ready',
          configuredFields: ['baseUrl', 'apiVersion'],
          missingFields: [],
          settings: [{ key: 'baseUrl', value: 'http://localhost:8080', required: true }],
          authMode: 'api-key', authConfigured: true,
        },
        health: { providerId: 'rest-transport-adapter', providerName: 'REST Transport', status: 'Healthy', checkedAt: new Date().toISOString(), summary: 'Connected', signals: ['connected'] },
        syncStatus: { providerId: 'rest-transport-adapter', mode: 'live', lastSuccessfulSyncAt: new Date().toISOString(), lastAttemptedSyncAt: new Date().toISOString(), status: 'healthy', detail: null },
        capabilities: { canRead: true, canWrite: false, canStreamEvents: false, canIngestCommands: false, canQueryHistory: false, supportsReadOnlyMode: true, canReplayData: false },
        schema: { providerId: 'rest-transport-adapter', resourceName: 'route', fields: [{ name: 'reference', type: 'string', required: true, canonicalMapping: 'Route.Reference', description: 'Route reference' }] },
        supportedReadModels: ['RouteSummaryReadModel', 'RouteDetailReadModel'],
      },
    ],
  };
}

export function mockGpsBoard() {
  return {
    generatedAtUtc: new Date().toISOString(),
    positions: [
      {
        truckId: 't-001', truckReference: 'TRUCK-01', latitude: 52.52, longitude: 13.40, speedKmph: 65,
        headingDegrees: 180, capturedAtUtc: new Date().toISOString(), ageMinutes: 2,
        movement: 'moving', routeId: 'r-001', routeReference: 'E2E-RT-1',
        alert: null, alertSummary: null,
      },
    ],
    summary: { totalTrucks: 1, movingTrucks: 1, idleTrucks: 0, staleTrucks: 0, speedingTrucks: 0, attentionCount: 0, staleCount: 0, routeLinkedCount: 1, summary: '1 truck moving' },
    focusTarget: null,
  };
}
