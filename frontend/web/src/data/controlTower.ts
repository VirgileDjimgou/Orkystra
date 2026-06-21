export type WarehouseZoneView = {
  code: string
  status: 'Stable' | 'Watch' | 'Critical'
  description: string
  utilization: number
  pallets: number
  throughputLabel: string
}

export type WarehouseSummaryView = {
  warehouseId: string
  name: string
  zoneCount: number
  rackCount: number
  slotCount: number
  occupiedDockCount: number
  storedPalletCount: number
  zones: WarehouseZoneView[]
}

export type RouteSummaryView = {
  routeId: string
  reference: string
  truckId: string
  truckReference: string
  status: 'On time' | 'At risk' | 'Delayed'
  stopCount: number
  shipmentCount: number
  completedDeliveryCount: number
  nextEtaLabel: string
  mapX: string
  mapY: string
}

export type ScenarioSummaryView = {
  scenarioId: string
  name: string
  seed: number
  status: 'Running' | 'Paused' | 'Completed'
  currentTimeLabel: string
  injectedEventCount: number
  mode: string
  outcomeDelta: string
  confidenceLabel: string
}

export type AlertView = {
  severity: 'Critical' | 'Warning' | 'Info'
  title: string
  description: string
}

export type EventFeedView = {
  id: string
  timeLabel: string
  title: string
  description: string
}

export type ProviderCatalogFieldView = {
  name: string
  canonicalMapping: string
  required: boolean
}

export type ProviderCatalogItemView = {
  providerId: string
  providerName: string
  domain: string
  kind: string
  healthStatus: 'Healthy' | 'Degraded' | 'Unhealthy'
  syncStatusLabel: string
  lastActivityLabel: string
  summary: string
  capabilities: string[]
  supportedReadModels: string[]
  schemaResourceName: string
  schemaFields: ProviderCatalogFieldView[]
}

export type ProviderStatusView = {
  providerId: string
  providerName: string
  domain: string
  healthStatus: 'Healthy' | 'Degraded' | 'Unhealthy'
  syncStatus: string
  syncStatusLabel: string
  lastActivityLabel: string
  summary: string
}

export type ControlTowerOverviewView = {
  tenantId: string
  generatedAtLabel: string
  scenarios: ScenarioSummaryView[]
  warehouses: WarehouseSummaryView[]
  routes: RouteSummaryView[]
  alerts: AlertView[]
  eventFeed: EventFeedView[]
  providers: ProviderStatusView[]
}

export type ProviderCatalogView = {
  generatedAtLabel: string
  providers: ProviderCatalogItemView[]
}

type ApiScenario = {
  scenarioId: string
  name: string
  seed: number
  status: string
  currentTime: string
  injectedEventCount: number
}

type ApiWarehouse = {
  warehouseId: string
  name: string
  zoneCount: number
  rackCount: number
  slotCount: number
  occupiedDockCount: number
  storedPalletCount: number
}

type ApiRoute = {
  routeId: string
  reference: string
  truckId: string
  truckReference: string
  status: string
  stopCount: number
  shipmentCount: number
  completedDeliveryCount: number
}

type ApiAlert = {
  severity: string
  title: string
  description: string
}

type ApiEventFeed = {
  eventId: string
  timeLabel: string
  title: string
  description: string
}

type ApiControlTowerOverview = {
  tenantId: string
  generatedAtUtc: string
  scenarios: ApiScenario[]
  warehouses: ApiWarehouse[]
  routes: ApiRoute[]
  alerts: ApiAlert[]
  eventFeed: ApiEventFeed[]
  providers: ApiProviderStatus[]
}

type ApiProviderStatus = {
  providerId: string
  providerName: string
  domain: string
  healthStatus: string
  syncStatus: string
  lastSuccessfulSyncAt: string | null
  lastAttemptedSyncAt: string | null
  summary: string
}

type ApiProviderCatalogField = {
  name: string
  type: string
  required: boolean
  canonicalMapping: string
  description: string
}

type ApiProviderCatalogItem = {
  providerId: string
  providerName: string
  domain: string
  kind: string
  health: {
    providerId: string
    providerName: string
    status: string
    checkedAt: string
    summary: string
    signals: string[]
  }
  syncStatus: {
    providerId: string
    mode: string
    lastSuccessfulSyncAt: string | null
    lastAttemptedSyncAt: string | null
    status: string
    detail: string | null
  }
  capabilities: {
    canRead: boolean
    canWrite: boolean
    canStreamEvents: boolean
    canIngestCommands: boolean
    canQueryHistory: boolean
    supportsReadOnlyMode: boolean
    canReplayData: boolean
  }
  schema: {
    providerId: string
    resourceName: string
    fields: ApiProviderCatalogField[]
  }
  supportedReadModels: string[]
}

type ApiProviderCatalogResponse = {
  generatedAtUtc: string
  providers: ApiProviderCatalogItem[]
}

export const simulationSpeeds = [
  { label: '1x', multiplier: 1 },
  { label: '4x', multiplier: 4 },
  { label: '16x', multiplier: 16 },
]

const zoneDecorations: Record<string, WarehouseZoneView[]> = {
  'North Hub A': [
    { code: 'INB', status: 'Stable', description: 'Inbound pallets waiting for slotting decisions.', utilization: 62, pallets: 124, throughputLabel: '38 pallets/h' },
    { code: 'AMB', status: 'Watch', description: 'Ambient picking wave with rising congestion.', utilization: 81, pallets: 228, throughputLabel: '57 picks/h' },
    { code: 'COL', status: 'Stable', description: 'Cold chain reserve with stable replenishment rhythm.', utilization: 54, pallets: 93, throughputLabel: '19 pallets/h' },
    { code: 'XDK', status: 'Critical', description: 'Cross-dock zone impacted by late carrier arrival.', utilization: 92, pallets: 167, throughputLabel: '12 trucks queued' },
  ],
  'West Flow Center': [
    { code: 'RET', status: 'Stable', description: 'Returns lane with controlled backlog.', utilization: 45, pallets: 88, throughputLabel: '23 cases/h' },
    { code: 'FUL', status: 'Watch', description: 'E-commerce fulfillment under peak order burst.', utilization: 79, pallets: 205, throughputLabel: '91 lines/h' },
    { code: 'STG', status: 'Stable', description: 'Outbound staging synced with carrier windows.', utilization: 63, pallets: 108, throughputLabel: '7 trailers/h' },
  ],
}

const routeDecorations: Record<string, { nextEtaLabel: string; mapX: string; mapY: string }> = {
  'RT-204': { nextEtaLabel: 'ETA 10:42', mapX: '16%', mapY: '58%' },
  'RT-318': { nextEtaLabel: 'ETA 11:05', mapX: '48%', mapY: '34%' },
  'RT-412': { nextEtaLabel: 'ETA 11:37', mapX: '76%', mapY: '68%' },
}

const scenarioDecorations: Record<string, { mode: string; outcomeDelta: string; confidenceLabel: string }> = {
  'Baseline day shift': { mode: 'Nominal', outcomeDelta: '+2.4% throughput', confidenceLabel: 'High confidence' },
  'Dock saturation stress': { mode: 'Stress', outcomeDelta: '-8.1% service level', confidenceLabel: 'Medium confidence' },
  'Late carrier recovery': { mode: 'Recovery', outcomeDelta: '+11 min ETA gain', confidenceLabel: 'High confidence' },
}

function normalizeScenarioStatus(status: string): ScenarioSummaryView['status'] {
  if (status === 'Paused' || status === 'Completed') {
    return status
  }
  return 'Running'
}

function normalizeRouteStatus(status: string): RouteSummaryView['status'] {
  if (status === 'Delayed' || status === 'At risk') {
    return status
  }
  return 'On time'
}

function normalizeAlertSeverity(severity: string): AlertView['severity'] {
  if (severity === 'Critical' || severity === 'Warning') {
    return severity
  }
  return 'Info'
}

function normalizeProviderHealthStatus(status: string): ProviderStatusView['healthStatus'] {
  if (status === 'Degraded' || status === 'Unhealthy') {
    return status
  }

  return 'Healthy'
}

function formatUtcLabel(value: string): string {
  const date = new Date(value)
  return `${date.getUTCFullYear()}-${String(date.getUTCMonth() + 1).padStart(2, '0')}-${String(date.getUTCDate()).padStart(2, '0')} ${String(date.getUTCHours()).padStart(2, '0')}:${String(date.getUTCMinutes()).padStart(2, '0')} UTC`
}

function formatRelativeSyncLabel(lastSuccessfulSyncAt: string | null, lastAttemptedSyncAt: string | null): string {
  if (lastSuccessfulSyncAt) {
    return `Last success ${formatUtcLabel(lastSuccessfulSyncAt)}`
  }

  if (lastAttemptedSyncAt) {
    return `Last attempt ${formatUtcLabel(lastAttemptedSyncAt)}`
  }

  return 'No sync recorded yet'
}

function formatSyncStatusLabel(syncStatus: string): string {
  return syncStatus
    .split('-')
    .map((segment) => segment.charAt(0).toUpperCase() + segment.slice(1))
    .join(' ')
}

function capabilityLabels(capabilities: ApiProviderCatalogItem['capabilities']): string[] {
  return [
    capabilities.canRead ? 'Read' : null,
    capabilities.canWrite ? 'Write' : null,
    capabilities.canStreamEvents ? 'Stream' : null,
    capabilities.canIngestCommands ? 'Commands' : null,
    capabilities.canQueryHistory ? 'History' : null,
    capabilities.supportsReadOnlyMode ? 'Read-only' : null,
    capabilities.canReplayData ? 'Replay' : null,
  ].filter((value): value is string => value !== null)
}

export function buildFallbackOverview(): ControlTowerOverviewView {
  return {
    tenantId: 'north-hub-demo',
    generatedAtLabel: '2026-06-20 10:15 UTC',
    scenarios: [
      {
        scenarioId: '9d4e8f09-cf15-48d8-90a6-e96c833fd741',
        name: 'Baseline day shift',
        seed: 42,
        status: 'Running',
        currentTimeLabel: '2026-06-20 10:15 UTC',
        injectedEventCount: 2,
        mode: 'Nominal',
        outcomeDelta: '+2.4% throughput',
        confidenceLabel: 'High confidence',
      },
      {
        scenarioId: '4172df1e-3fb6-4d56-b04c-775b9fcd8620',
        name: 'Dock saturation stress',
        seed: 77,
        status: 'Paused',
        currentTimeLabel: '2026-06-20 11:05 UTC',
        injectedEventCount: 5,
        mode: 'Stress',
        outcomeDelta: '-8.1% service level',
        confidenceLabel: 'Medium confidence',
      },
      {
        scenarioId: '0a59d24d-b1fc-45dd-9000-508862c4af53',
        name: 'Late carrier recovery',
        seed: 103,
        status: 'Completed',
        currentTimeLabel: '2026-06-20 13:40 UTC',
        injectedEventCount: 3,
        mode: 'Recovery',
        outcomeDelta: '+11 min ETA gain',
        confidenceLabel: 'High confidence',
      },
    ],
    warehouses: [
      {
        warehouseId: 'db9a789f-9df8-45ff-a252-96d4319c2f12',
        name: 'North Hub A',
        zoneCount: 4,
        rackCount: 18,
        slotCount: 820,
        occupiedDockCount: 3,
        storedPalletCount: 612,
        zones: zoneDecorations['North Hub A'],
      },
      {
        warehouseId: '3f224c42-00a5-49a6-955c-c8114d0a6b81',
        name: 'West Flow Center',
        zoneCount: 3,
        rackCount: 14,
        slotCount: 640,
        occupiedDockCount: 2,
        storedPalletCount: 401,
        zones: zoneDecorations['West Flow Center'],
      },
    ],
    routes: [
      {
        routeId: '5024fa82-f658-46c8-88bf-aece07d56f09',
        reference: 'RT-204',
        truckId: '0d91dc2f-3a74-4562-96a6-c8de611f699d',
        truckReference: 'TRK-11',
        status: 'On time',
        stopCount: 5,
        shipmentCount: 22,
        completedDeliveryCount: 2,
        nextEtaLabel: 'ETA 10:42',
        mapX: '16%',
        mapY: '58%',
      },
      {
        routeId: '528c1588-40fd-451b-8c86-2caa625602de',
        reference: 'RT-318',
        truckId: '2a398a30-61cf-4fc3-a18d-e491530b4f24',
        truckReference: 'TRK-07',
        status: 'At risk',
        stopCount: 4,
        shipmentCount: 15,
        completedDeliveryCount: 1,
        nextEtaLabel: 'ETA 11:05',
        mapX: '48%',
        mapY: '34%',
      },
      {
        routeId: '9f91e82e-226a-48f7-a94c-907b79431739',
        reference: 'RT-412',
        truckId: 'cf7c6cc8-7b55-49d4-94ff-a5ee9e340856',
        truckReference: 'TRK-19',
        status: 'Delayed',
        stopCount: 6,
        shipmentCount: 27,
        completedDeliveryCount: 3,
        nextEtaLabel: 'ETA 11:37',
        mapX: '76%',
        mapY: '68%',
      },
    ],
    alerts: [
      { severity: 'Critical', title: 'Cross-dock queue building', description: 'Carrier handoff at North Hub A is slipping beyond the planned window for route RT-412.' },
      { severity: 'Warning', title: 'Ambient picking under pressure', description: 'Wave density in zone AMB is 14% above the modeled baseline for this hour.' },
      { severity: 'Info', title: 'Scenario branch available', description: 'The current simulation can fork from 10:15 UTC to test an alternate carrier assignment.' },
    ],
    eventFeed: [
      { id: 'evt-1', timeLabel: '10:11', title: 'ScenarioStarted', description: 'Baseline day shift resumed with deterministic seed 42.' },
      { id: 'evt-2', timeLabel: '10:13', title: 'TruckDelayed', description: 'TRK-19 reported a 17 minute delay on route RT-412 after urban congestion.' },
      { id: 'evt-3', timeLabel: '10:14', title: 'RandomEventInjected', description: 'Forklift maintenance created a temporary cross-dock bottleneck in zone XDK.' },
      { id: 'evt-4', timeLabel: '10:15', title: 'TimeAdvanced', description: 'Simulation clock advanced the operational state to the next wave checkpoint.' },
    ],
    providers: [
      {
        providerId: 'csv-warehouse-import',
        providerName: 'CSV Warehouse Import',
        domain: 'Warehouse',
        healthStatus: 'Healthy',
        syncStatus: 'idle',
        syncStatusLabel: 'Idle',
        lastActivityLabel: 'No sync recorded yet',
        summary: 'CSV adapter skeleton is ready to validate and map warehouse import files.',
      },
      {
        providerId: 'rest-transport-adapter',
        providerName: 'REST Transport Adapter',
        domain: 'Transport',
        healthStatus: 'Degraded',
        syncStatus: 'awaiting-configuration',
        syncStatusLabel: 'Awaiting Configuration',
        lastActivityLabel: 'Last attempt 2026-06-20 10:15 UTC',
        summary: 'REST adapter skeleton is available but not yet configured against a live upstream service.',
      },
      {
        providerId: 'gps-telematics-adapter',
        providerName: 'GPS Telematics Adapter',
        domain: 'Gps',
        healthStatus: 'Healthy',
        syncStatus: 'connected',
        syncStatusLabel: 'Connected',
        lastActivityLabel: 'Last success 2026-06-20 10:14 UTC',
        summary: 'GPS adapter skeleton can expose canonical truck-position snapshots.',
      },
    ],
  }
}

export function mapApiOverviewToView(apiOverview: ApiControlTowerOverview): ControlTowerOverviewView {
  return {
    tenantId: apiOverview.tenantId,
    generatedAtLabel: formatUtcLabel(apiOverview.generatedAtUtc),
    scenarios: apiOverview.scenarios.map((scenario) => ({
      scenarioId: scenario.scenarioId,
      name: scenario.name,
      seed: scenario.seed,
      status: normalizeScenarioStatus(scenario.status),
      currentTimeLabel: formatUtcLabel(scenario.currentTime),
      injectedEventCount: scenario.injectedEventCount,
      mode: scenarioDecorations[scenario.name]?.mode ?? 'Nominal',
      outcomeDelta: scenarioDecorations[scenario.name]?.outcomeDelta ?? 'Pending analysis',
      confidenceLabel: scenarioDecorations[scenario.name]?.confidenceLabel ?? 'Medium confidence',
    })),
    warehouses: apiOverview.warehouses.map((warehouse) => ({
      warehouseId: warehouse.warehouseId,
      name: warehouse.name,
      zoneCount: warehouse.zoneCount,
      rackCount: warehouse.rackCount,
      slotCount: warehouse.slotCount,
      occupiedDockCount: warehouse.occupiedDockCount,
      storedPalletCount: warehouse.storedPalletCount,
      zones: zoneDecorations[warehouse.name] ?? [],
    })),
    routes: apiOverview.routes.map((route) => ({
      routeId: route.routeId,
      reference: route.reference,
      truckId: route.truckId,
      truckReference: route.truckReference,
      status: normalizeRouteStatus(route.status),
      stopCount: route.stopCount,
      shipmentCount: route.shipmentCount,
      completedDeliveryCount: route.completedDeliveryCount,
      nextEtaLabel: routeDecorations[route.reference]?.nextEtaLabel ?? 'ETA pending',
      mapX: routeDecorations[route.reference]?.mapX ?? '50%',
      mapY: routeDecorations[route.reference]?.mapY ?? '50%',
    })),
    alerts: apiOverview.alerts.map((alert) => ({
      severity: normalizeAlertSeverity(alert.severity),
      title: alert.title,
      description: alert.description,
    })),
    eventFeed: apiOverview.eventFeed.map((event) => ({
      id: event.eventId,
      timeLabel: event.timeLabel,
      title: event.title,
      description: event.description,
    })),
    providers: apiOverview.providers.map((provider) => ({
      providerId: provider.providerId,
      providerName: provider.providerName,
      domain: provider.domain,
      healthStatus: normalizeProviderHealthStatus(provider.healthStatus),
      syncStatus: provider.syncStatus,
      syncStatusLabel: formatSyncStatusLabel(provider.syncStatus),
      lastActivityLabel: formatRelativeSyncLabel(provider.lastSuccessfulSyncAt, provider.lastAttemptedSyncAt),
      summary: provider.summary,
    })),
  }
}

export function buildFallbackProviderCatalog(): ProviderCatalogView {
  return {
    generatedAtLabel: '2026-06-20 10:15 UTC',
    providers: [
      {
        providerId: 'csv-warehouse-import',
        providerName: 'CSV Warehouse Import',
        domain: 'Warehouse',
        kind: 'Connector',
        healthStatus: 'Healthy',
        syncStatusLabel: 'Ready',
        lastActivityLabel: 'Last success 2026-06-20 09:57 UTC',
        summary: 'CSV adapter skeleton is ready to validate and map warehouse import files.',
        capabilities: ['Read', 'Read-only', 'Replay'],
        supportedReadModels: ['WarehouseSummaryReadModel'],
        schemaResourceName: 'warehouse-csv-row',
        schemaFields: [
          { name: 'warehouse_name', canonicalMapping: 'Warehouse.Name', required: true },
          { name: 'zone_count', canonicalMapping: 'Warehouse.ZoneCount', required: true },
          { name: 'rack_count', canonicalMapping: 'Warehouse.RackCount', required: true },
          { name: 'slot_count', canonicalMapping: 'Warehouse.SlotCount', required: true },
        ],
      },
      {
        providerId: 'rest-transport-adapter',
        providerName: 'REST Transport Adapter',
        domain: 'Transport',
        kind: 'Connector',
        healthStatus: 'Degraded',
        syncStatusLabel: 'Awaiting Configuration',
        lastActivityLabel: 'Last attempt 2026-06-20 10:15 UTC',
        summary: 'REST adapter skeleton is available but not yet configured against a live upstream service.',
        capabilities: ['Read', 'Write', 'Commands', 'History', 'Read-only'],
        supportedReadModels: ['RouteSummaryReadModel'],
        schemaResourceName: 'transport-route-resource',
        schemaFields: [
          { name: 'route_reference', canonicalMapping: 'Route.Reference', required: true },
          { name: 'truck_reference', canonicalMapping: 'Truck.Reference', required: true },
          { name: 'status', canonicalMapping: 'Route.Status', required: true },
          { name: 'stop_count', canonicalMapping: 'Route.StopCount', required: true },
        ],
      },
      {
        providerId: 'gps-telematics-adapter',
        providerName: 'GPS Telematics Adapter',
        domain: 'Gps',
        kind: 'Connector',
        healthStatus: 'Healthy',
        syncStatusLabel: 'Connected',
        lastActivityLabel: 'Last success 2026-06-20 10:14 UTC',
        summary: 'GPS adapter skeleton can expose canonical truck-position snapshots.',
        capabilities: ['Read', 'Stream', 'History', 'Read-only', 'Replay'],
        supportedReadModels: ['GpsPositionSnapshot'],
        schemaResourceName: 'gps-position-event',
        schemaFields: [
          { name: 'truck_reference', canonicalMapping: 'Truck.Reference', required: true },
          { name: 'latitude', canonicalMapping: 'GpsPosition.Latitude', required: true },
          { name: 'longitude', canonicalMapping: 'GpsPosition.Longitude', required: true },
          { name: 'speed_kph', canonicalMapping: 'GpsPosition.SpeedKph', required: false },
        ],
      },
    ],
  }
}

export function mapApiProviderCatalogToView(apiCatalog: ApiProviderCatalogResponse): ProviderCatalogView {
  return {
    generatedAtLabel: formatUtcLabel(apiCatalog.generatedAtUtc),
    providers: apiCatalog.providers.map((provider) => ({
      providerId: provider.providerId,
      providerName: provider.providerName,
      domain: provider.domain,
      kind: provider.kind,
      healthStatus: normalizeProviderHealthStatus(provider.health.status),
      syncStatusLabel: formatSyncStatusLabel(provider.syncStatus.status),
      lastActivityLabel: formatRelativeSyncLabel(provider.syncStatus.lastSuccessfulSyncAt, provider.syncStatus.lastAttemptedSyncAt),
      summary: provider.health.summary,
      capabilities: capabilityLabels(provider.capabilities),
      supportedReadModels: provider.supportedReadModels,
      schemaResourceName: provider.schema.resourceName,
      schemaFields: provider.schema.fields.map((field) => ({
        name: field.name,
        canonicalMapping: field.canonicalMapping,
        required: field.required,
      })),
    })),
  }
}
