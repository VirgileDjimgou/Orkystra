export type WarehouseZoneView = {
  code: string
  name: string
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
}

export type WarehouseDockView = {
  code: string
  status: 'Available' | 'Occupied'
  activityLabel: string
}

export type WarehouseDetailView = {
  warehouseId: string
  name: string
  zoneCount: number
  rackCount: number
  slotCount: number
  occupiedDockCount: number
  storedPalletCount: number
  updatedAtLabel: string
  zones: WarehouseZoneView[]
  docks: WarehouseDockView[]
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

export type RouteStopView = {
  sequence: number
  name: string
  coordinateLabel: string
  timeWindowLabel: string
}

export type RouteShipmentView = {
  reference: string
  status: 'Created' | 'Assigned' | 'Loaded' | 'Departed' | 'Arrived' | 'Completed'
  loadWeightKilograms: number
  orderReference: string
}

export type RouteDeliveryView = {
  reference: string
  stopSequence: number
  stopName: string
  shipmentReference: string
  status: 'Pending' | 'Completed'
}

export type RouteDetailView = {
  routeId: string
  reference: string
  truckId: string
  truckReference: string
  driverName: string
  status: 'On time' | 'At risk' | 'Delayed'
  truckStatus: string
  truckCapacityKilograms: number
  totalLoadKilograms: number
  stopCount: number
  shipmentCount: number
  completedDeliveryCount: number
  updatedAtLabel: string
  stops: RouteStopView[]
  shipments: RouteShipmentView[]
  deliveries: RouteDeliveryView[]
}

export type TransportSyncStatusView = {
  providerId: string
  source: 'live' | 'demo-fallback' | 'configuration-incomplete' | 'disabled'
  liveImport: boolean
  hasPersistedSnapshot: boolean
  importedRouteCount: number
  importedRouteIds: string[]
  importedRouteReferences: string[]
  lastImportedAtUtc: string | null
  lastSuccessfulSyncAtUtc: string | null
  lastAttemptedSyncAtUtc: string | null
  lastImportedAtLabel: string | null
  lastActivityLabel: string
  syncStatus: string
  syncStatusLabel: string
  syncDetail: string | null
  healthStatus: 'Healthy' | 'Degraded' | 'Unhealthy'
  healthSummary: string
}

export type TransportSyncRouteDiffItemView = {
  routeReference: string
  changeType: 'Added' | 'Removed' | 'Changed' | 'Unchanged'
  previousStatus: string | null
  currentStatus: string | null
  previousStopCount: number | null
  currentStopCount: number | null
  previousShipmentCount: number | null
  currentShipmentCount: number | null
  previousCompletedDeliveryCount: number | null
  currentCompletedDeliveryCount: number | null
  summary: string
}

export type TransportSyncDiffView = {
  hasComparableHistory: boolean
  detail: string
  latestImportedAtLabel: string | null
  previousImportedAtLabel: string | null
  latestRouteCount: number
  previousRouteCount: number
  addedRouteCount: number
  removedRouteCount: number
  changedRouteCount: number
  routeDiffs: TransportSyncRouteDiffItemView[]
}

export type TransportSyncHistoryEntryView = {
  id: string
  createdAtLabel: string
  importedAtLabel: string | null
  source: string
  status: string
  importedRouteCount: number
  healthStatus: 'Healthy' | 'Degraded' | 'Unhealthy'
  summary: string
  hasComparablePrevious: boolean
  addedRouteCount: number
  removedRouteCount: number
  changedRouteCount: number
  routeReferencePreview: string[]
}

export type TransportSyncHistoryView = {
  count: number
  summary: string
  entries: TransportSyncHistoryEntryView[]
}

export type TransportExceptionWorkbenchItemView = {
  id: string
  severity: 'Critical' | 'Warning' | 'Info'
  category: string
  title: string
  detail: string
  routeId: string | null
  routeReference: string | null
  recommendedAction: 'sync-import' | 'sync-refresh' | 'focus-route' | 'focus-route-diff' | 'optimization-refresh' | 'selected-diff' | 'review-history'
  actionLabel: string
  resolutionStatus: 'Reviewed' | 'Resolved' | 'Deferred' | null
  resolutionNote: string | null
  resolutionUpdatedAtLabel: string | null
  evidence: string[]
}

export type TransportExceptionWorkbenchGroupView = {
  groupKey: string
  label: string
  highestSeverity: 'Critical' | 'Warning' | 'Info'
  count: number
  summary: string
  recommendedAction: 'sync-import' | 'sync-refresh' | 'focus-route' | 'focus-route-diff' | 'optimization-refresh' | 'selected-diff' | 'review-history'
  actionLabel: string
}

export type TransportExceptionWorkbenchView = {
  generatedAtLabel: string
  exceptionCount: number
  summary: string
  groups: TransportExceptionWorkbenchGroupView[]
  items: TransportExceptionWorkbenchItemView[]
}

export type TransportExceptionResolutionHistoryEntryView = {
  id: string
  exceptionId: string
  status: 'Reviewed' | 'Resolved' | 'Deferred'
  note: string | null
  updatedAtLabel: string
}

export type TransportExceptionResolutionHistoryView = {
  count: number
  summary: string
  entries: TransportExceptionResolutionHistoryEntryView[]
}

export type RouteOptimizationAlternativeView = {
  label: string
  orderedStopReferences: string[]
  objectiveScore: number
  summary: string
}

export type RouteOptimizationView = {
  routeId: string
  routeReference: string
  status: 'optimized' | 'infeasible'
  objectiveScore: number | null
  orderedStopReferences: string[]
  etaMinutes: Record<string, number>
  loadDistribution: Record<string, number>
  constraintViolations: string[]
  explanation: {
    selectedVehicleReason: string
    prioritizationReason: string
    tightConstraints: string[]
    infeasibilityReason: string | null
    tradeOffs: string[]
  }
  alternatives: RouteOptimizationAlternativeView[]
  solverBackend: string
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

export type ProviderConfigurationSettingView = {
  key: string
  value: string
  required: boolean
}

export type ProviderCatalogItemView = {
  providerId: string
  providerName: string
  domain: string
  kind: string
  healthStatus: 'Healthy' | 'Degraded' | 'Unhealthy'
  configurationReadiness: string
  configurationEnvironment: string
  configurationEnabled: boolean
  configuredFields: string[]
  missingFields: string[]
  editableSettings: ProviderConfigurationSettingView[]
  authMode: string
  authConfigured: boolean
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

type ApiWarehouseZone = {
  code: string
  name: string
  status: string
  description: string
  utilizationPercent: number
  palletCount: number
  throughputLabel: string
}

type ApiWarehouseDock = {
  code: string
  status: string
  activityLabel: string
}

type ApiWarehouseDetail = {
  warehouseId: string
  name: string
  zoneCount: number
  rackCount: number
  slotCount: number
  occupiedDockCount: number
  storedPalletCount: number
  updatedAtUtc: string
  zones: ApiWarehouseZone[]
  docks: ApiWarehouseDock[]
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

type ApiRouteStop = {
  sequence: number
  name: string
  coordinateLabel: string
  timeWindowLabel: string
}

type ApiRouteShipment = {
  reference: string
  status: string
  loadWeightKilograms: number
  orderReference: string
}

type ApiRouteDelivery = {
  reference: string
  stopSequence: number
  stopName: string
  shipmentReference: string
  status: string
}

type ApiRouteDetail = {
  routeId: string
  reference: string
  truckId: string
  truckReference: string
  driverName: string
  status: string
  truckStatus: string
  truckCapacityKilograms: number
  totalLoadKilograms: number
  stopCount: number
  shipmentCount: number
  completedDeliveryCount: number
  updatedAtUtc: string
  stops: ApiRouteStop[]
  shipments: ApiRouteShipment[]
  deliveries: ApiRouteDelivery[]
}

type ApiTransportSyncStatus = {
  providerId: string
  source: string
  liveImport: boolean
  hasPersistedSnapshot: boolean
  importedRouteCount: number
  importedRouteIds: string[]
  importedRouteReferences: string[]
  lastImportedAtUtc: string | null
  lastSuccessfulSyncAt: string | null
  lastAttemptedSyncAt: string | null
  syncStatus: string
  syncDetail: string | null
  health: {
    providerId: string
    providerName: string
    status: string
    checkedAt: string
    summary: string
    signals: string[]
  }
}

type ApiTransportSyncRouteDiffItem = {
  routeReference: string
  previousRouteId: string | null
  currentRouteId: string | null
  changeType: string
  previousStatus: string | null
  currentStatus: string | null
  previousStopCount: number | null
  currentStopCount: number | null
  previousShipmentCount: number | null
  currentShipmentCount: number | null
  previousCompletedDeliveryCount: number | null
  currentCompletedDeliveryCount: number | null
  summary: string
}

type ApiTransportSyncDiff = {
  hasComparableHistory: boolean
  detail: string
  latestImportedAtUtc: string | null
  previousImportedAtUtc: string | null
  latestRouteCount: number
  previousRouteCount: number
  addedRouteCount: number
  removedRouteCount: number
  changedRouteCount: number
  routeDiffs: ApiTransportSyncRouteDiffItem[]
}

type ApiRouteOptimizationAlternative = {
  label: string
  orderedStopReferences: string[]
  objectiveScore: number
  summary: string
}

type ApiRouteOptimizationResult = {
  routeId: string
  routeReference: string
  status: string
  objectiveScore: number | null
  orderedStopReferences: string[]
  etaMinutes: Record<string, number>
  loadDistribution: Record<string, number>
  constraintViolations: string[]
  explanation: {
    selectedVehicleReason: string
    prioritizationReason: string
    tightConstraints: string[]
    infeasibilityReason: string | null
    tradeOffs: string[]
  }
  alternatives: ApiRouteOptimizationAlternative[]
  solverBackend: string
}

type ApiRouteOptimizationEnvelope = {
  optimization: ApiRouteOptimizationResult
  source: string
  errorMessage: string | null
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
  configuration: {
    enabled: boolean
    environment: string
    readiness: string
    configuredFields: string[]
    missingFields: string[]
    settings: Array<{
      key: string
      value: string
      required: boolean
    }>
    authMode: string
    authConfigured: boolean
  }
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

const fallbackWarehouseDetails: Record<string, WarehouseDetailView> = {
  'db9a789f-9df8-45ff-a252-96d4319c2f12': {
    warehouseId: 'db9a789f-9df8-45ff-a252-96d4319c2f12',
    name: 'North Hub A',
    zoneCount: 4,
    rackCount: 18,
    slotCount: 820,
    occupiedDockCount: 3,
    storedPalletCount: 612,
    updatedAtLabel: '2026-06-20 10:15 UTC',
    zones: [
      { code: 'INB', name: 'Inbound Buffer', status: 'Stable', description: 'Inbound pallets waiting for slotting decisions.', utilization: 62, pallets: 124, throughputLabel: '38 pallets/h' },
      { code: 'AMB', name: 'Ambient Picking', status: 'Watch', description: 'Ambient picking wave with rising congestion.', utilization: 81, pallets: 228, throughputLabel: '57 picks/h' },
      { code: 'COL', name: 'Cold Reserve', status: 'Stable', description: 'Cold chain reserve with stable replenishment rhythm.', utilization: 54, pallets: 93, throughputLabel: '19 pallets/h' },
      { code: 'XDK', name: 'Cross Dock', status: 'Critical', description: 'Cross-dock zone impacted by late carrier arrival.', utilization: 92, pallets: 167, throughputLabel: '12 trucks queued' },
    ],
    docks: [
      { code: 'D-01', status: 'Occupied', activityLabel: 'Trailer TRK-19 staging late handoff' },
      { code: 'D-02', status: 'Occupied', activityLabel: 'Outbound wave RT-204 loading' },
      { code: 'D-03', status: 'Occupied', activityLabel: 'Inbound unload slot active' },
      { code: 'D-04', status: 'Available', activityLabel: 'Buffer dock ready' },
    ],
  },
  '3f224c42-00a5-49a6-955c-c8114d0a6b81': {
    warehouseId: '3f224c42-00a5-49a6-955c-c8114d0a6b81',
    name: 'West Flow Center',
    zoneCount: 3,
    rackCount: 14,
    slotCount: 640,
    occupiedDockCount: 2,
    storedPalletCount: 401,
    updatedAtLabel: '2026-06-20 10:12 UTC',
    zones: [
      { code: 'RET', name: 'Returns', status: 'Stable', description: 'Returns lane with controlled backlog.', utilization: 45, pallets: 88, throughputLabel: '23 cases/h' },
      { code: 'FUL', name: 'Fulfillment', status: 'Watch', description: 'E-commerce fulfillment under peak order burst.', utilization: 79, pallets: 205, throughputLabel: '91 lines/h' },
      { code: 'STG', name: 'Staging', status: 'Stable', description: 'Outbound staging synced with carrier windows.', utilization: 63, pallets: 108, throughputLabel: '7 trailers/h' },
    ],
    docks: [
      { code: 'W-01', status: 'Occupied', activityLabel: 'Parcel wave consolidation' },
      { code: 'W-02', status: 'Occupied', activityLabel: 'Carrier arrival on schedule' },
      { code: 'W-03', status: 'Available', activityLabel: 'Returns overflow backup' },
    ],
  },
}

const fallbackRouteDetails: Record<string, RouteDetailView> = {
  '5024fa82-f658-46c8-88bf-aece07d56f09': {
    routeId: '5024fa82-f658-46c8-88bf-aece07d56f09',
    reference: 'RT-204',
    truckId: '0d91dc2f-3a74-4562-96a6-c8de611f699d',
    truckReference: 'TRK-11',
    driverName: 'Alex Driver',
    status: 'On time',
    truckStatus: 'In transit',
    truckCapacityKilograms: 500,
    totalLoadKilograms: 440,
    stopCount: 5,
    shipmentCount: 22,
    completedDeliveryCount: 2,
    updatedAtLabel: '2026-06-20 10:42 UTC',
    stops: [
      { sequence: 1, name: 'North Hub A', coordinateLabel: '48.8566, 2.3522', timeWindowLabel: '08:00-09:30' },
      { sequence: 2, name: 'City Cross-Dock', coordinateLabel: '48.8809, 2.3743', timeWindowLabel: '09:45-10:15' },
      { sequence: 3, name: 'Retail Depot 14', coordinateLabel: '48.9050, 2.4130', timeWindowLabel: '10:30-11:15' },
      { sequence: 4, name: 'Retail Depot 19', coordinateLabel: '48.9178, 2.4571', timeWindowLabel: '11:20-12:10' },
      { sequence: 5, name: 'West Flow Center', coordinateLabel: '48.9352, 2.4912', timeWindowLabel: '12:20-13:00' },
    ],
    shipments: [
      { reference: 'SHIP-204-01', status: 'Loaded', loadWeightKilograms: 120, orderReference: 'ORD-2041' },
      { reference: 'SHIP-204-02', status: 'Departed', loadWeightKilograms: 100, orderReference: 'ORD-2042' },
      { reference: 'SHIP-204-03', status: 'Arrived', loadWeightKilograms: 80, orderReference: 'ORD-2043' },
      { reference: 'SHIP-204-04', status: 'Completed', loadWeightKilograms: 140, orderReference: 'ORD-2044' },
    ],
    deliveries: [
      { reference: 'DLV-204-01', stopSequence: 1, stopName: 'North Hub A', shipmentReference: 'SHIP-204-01', status: 'Completed' },
      { reference: 'DLV-204-02', stopSequence: 2, stopName: 'City Cross-Dock', shipmentReference: 'SHIP-204-02', status: 'Completed' },
      { reference: 'DLV-204-03', stopSequence: 3, stopName: 'Retail Depot 14', shipmentReference: 'SHIP-204-03', status: 'Pending' },
      { reference: 'DLV-204-04', stopSequence: 4, stopName: 'Retail Depot 19', shipmentReference: 'SHIP-204-04', status: 'Pending' },
      { reference: 'DLV-204-05', stopSequence: 5, stopName: 'West Flow Center', shipmentReference: 'SHIP-204-04', status: 'Pending' },
    ],
  },
  '528c1588-40fd-451b-8c86-2caa625602de': {
    routeId: '528c1588-40fd-451b-8c86-2caa625602de',
    reference: 'RT-318',
    truckId: '2a398a30-61cf-4fc3-a18d-e491530b4f24',
    truckReference: 'TRK-07',
    driverName: 'Mina Lopez',
    status: 'At risk',
    truckStatus: 'Delayed',
    truckCapacityKilograms: 460,
    totalLoadKilograms: 310,
    stopCount: 4,
    shipmentCount: 15,
    completedDeliveryCount: 1,
    updatedAtLabel: '2026-06-20 11:05 UTC',
    stops: [
      { sequence: 1, name: 'West Flow Center', coordinateLabel: '48.9352, 2.4912', timeWindowLabel: '09:10-09:40' },
      { sequence: 2, name: 'Retail Depot 21', coordinateLabel: '48.9642, 2.5339', timeWindowLabel: '10:00-10:30' },
      { sequence: 3, name: 'Regional Store 07', coordinateLabel: '48.9772, 2.5751', timeWindowLabel: '10:45-11:15' },
      { sequence: 4, name: 'Regional Store 11', coordinateLabel: '48.9918, 2.6182', timeWindowLabel: '11:30-12:20' },
    ],
    shipments: [
      { reference: 'SHIP-318-01', status: 'Loaded', loadWeightKilograms: 90, orderReference: 'ORD-3181' },
      { reference: 'SHIP-318-02', status: 'Loaded', loadWeightKilograms: 75, orderReference: 'ORD-3182' },
      { reference: 'SHIP-318-03', status: 'Departed', loadWeightKilograms: 80, orderReference: 'ORD-3183' },
      { reference: 'SHIP-318-04', status: 'Arrived', loadWeightKilograms: 65, orderReference: 'ORD-3184' },
    ],
    deliveries: [
      { reference: 'DLV-318-01', stopSequence: 1, stopName: 'West Flow Center', shipmentReference: 'SHIP-318-01', status: 'Completed' },
      { reference: 'DLV-318-02', stopSequence: 2, stopName: 'Retail Depot 21', shipmentReference: 'SHIP-318-02', status: 'Pending' },
      { reference: 'DLV-318-03', stopSequence: 3, stopName: 'Regional Store 07', shipmentReference: 'SHIP-318-03', status: 'Pending' },
      { reference: 'DLV-318-04', stopSequence: 4, stopName: 'Regional Store 11', shipmentReference: 'SHIP-318-04', status: 'Pending' },
    ],
  },
  '9f91e82e-226a-48f7-a94c-907b79431739': {
    routeId: '9f91e82e-226a-48f7-a94c-907b79431739',
    reference: 'RT-412',
    truckId: 'cf7c6cc8-7b55-49d4-94ff-a5ee9e340856',
    truckReference: 'TRK-19',
    driverName: 'Noah Karim',
    status: 'Delayed',
    truckStatus: 'Delayed',
    truckCapacityKilograms: 520,
    totalLoadKilograms: 470,
    stopCount: 6,
    shipmentCount: 27,
    completedDeliveryCount: 3,
    updatedAtLabel: '2026-06-20 11:37 UTC',
    stops: [
      { sequence: 1, name: 'North Hub A', coordinateLabel: '48.8566, 2.3522', timeWindowLabel: '09:20-09:50' },
      { sequence: 2, name: 'Urban Relay 04', coordinateLabel: '48.8893, 2.3784', timeWindowLabel: '10:15-10:45' },
      { sequence: 3, name: 'Regional Store 16', coordinateLabel: '48.9251, 2.4220', timeWindowLabel: '11:00-11:30' },
      { sequence: 4, name: 'Regional Store 22', coordinateLabel: '48.9517, 2.4613', timeWindowLabel: '11:40-12:05' },
      { sequence: 5, name: 'Cross-Dock Bravo', coordinateLabel: '48.9784, 2.5085', timeWindowLabel: '12:20-12:50' },
      { sequence: 6, name: 'West Flow Center', coordinateLabel: '48.9952, 2.5526', timeWindowLabel: '13:00-13:40' },
    ],
    shipments: [
      { reference: 'SHIP-412-01', status: 'Loaded', loadWeightKilograms: 80, orderReference: 'ORD-4121' },
      { reference: 'SHIP-412-02', status: 'Loaded', loadWeightKilograms: 90, orderReference: 'ORD-4122' },
      { reference: 'SHIP-412-03', status: 'Departed', loadWeightKilograms: 75, orderReference: 'ORD-4123' },
      { reference: 'SHIP-412-04', status: 'Departed', loadWeightKilograms: 95, orderReference: 'ORD-4124' },
      { reference: 'SHIP-412-05', status: 'Arrived', loadWeightKilograms: 65, orderReference: 'ORD-4125' },
      { reference: 'SHIP-412-06', status: 'Completed', loadWeightKilograms: 65, orderReference: 'ORD-4126' },
    ],
    deliveries: [
      { reference: 'DLV-412-01', stopSequence: 1, stopName: 'North Hub A', shipmentReference: 'SHIP-412-01', status: 'Completed' },
      { reference: 'DLV-412-02', stopSequence: 2, stopName: 'Urban Relay 04', shipmentReference: 'SHIP-412-02', status: 'Completed' },
      { reference: 'DLV-412-03', stopSequence: 3, stopName: 'Regional Store 16', shipmentReference: 'SHIP-412-03', status: 'Pending' },
      { reference: 'DLV-412-04', stopSequence: 4, stopName: 'Regional Store 22', shipmentReference: 'SHIP-412-04', status: 'Pending' },
      { reference: 'DLV-412-05', stopSequence: 5, stopName: 'Cross-Dock Bravo', shipmentReference: 'SHIP-412-05', status: 'Pending' },
      { reference: 'DLV-412-06', stopSequence: 6, stopName: 'West Flow Center', shipmentReference: 'SHIP-412-06', status: 'Pending' },
    ],
  },
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

function normalizeZoneStatus(status: string): WarehouseZoneView['status'] {
  if (status === 'Watch' || status === 'Critical') {
    return status
  }

  return 'Stable'
}

function normalizeDockStatus(status: string): WarehouseDockView['status'] {
  if (status === 'Occupied') {
    return status
  }

  return 'Available'
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

function normalizeTransportSyncSource(source: string): TransportSyncStatusView['source'] {
  if (
    source === 'live' ||
    source === 'demo-fallback' ||
    source === 'configuration-incomplete' ||
    source === 'disabled'
  ) {
    return source
  }

  return 'demo-fallback'
}

export function formatUtcLabel(value: string): string {
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

function buildWarehouseSummaryFromDetail(detail: WarehouseDetailView): WarehouseSummaryView {
  return {
    warehouseId: detail.warehouseId,
    name: detail.name,
    zoneCount: detail.zoneCount,
    rackCount: detail.rackCount,
    slotCount: detail.slotCount,
    occupiedDockCount: detail.occupiedDockCount,
    storedPalletCount: detail.storedPalletCount,
  }
}

function buildRouteSummaryFromDetail(detail: RouteDetailView): RouteSummaryView {
  return {
    routeId: detail.routeId,
    reference: detail.reference,
    truckId: detail.truckId,
    truckReference: detail.truckReference,
    status: detail.status,
    stopCount: detail.stopCount,
    shipmentCount: detail.shipmentCount,
    completedDeliveryCount: detail.completedDeliveryCount,
    nextEtaLabel: detail.reference === 'RT-204' ? 'ETA 10:42' : detail.reference === 'RT-318' ? 'ETA 11:05' : 'ETA 11:37',
    mapX: detail.reference === 'RT-204' ? '16%' : detail.reference === 'RT-318' ? '48%' : '76%',
    mapY: detail.reference === 'RT-204' ? '58%' : detail.reference === 'RT-318' ? '34%' : '68%',
  }
}

function pendingRouteStops(detail: RouteDetailView): Array<{ name: string; sequence: number; timeWindowLabel: string; priority: number }> {
  const pendingStopSequences = new Set(
    detail.deliveries
      .filter((delivery) => delivery.status !== 'Completed')
      .map((delivery) => delivery.stopSequence),
  )

  return detail.stops
    .filter((stop) => pendingStopSequences.has(stop.sequence))
    .map((stop, index) => ({
      name: stop.name,
      sequence: stop.sequence,
      timeWindowLabel: stop.timeWindowLabel,
      priority: Math.max(1, 10 - index),
    }))
}

function parseTimeWindowEndMinutes(label: string): number {
  const range = label.split('-')
  if (range.length !== 2) {
    return 0
  }

  const endParts = range[1].trim().split(':')
  if (endParts.length !== 2) {
    return 0
  }

  return (Number(endParts[0]) * 60) + Number(endParts[1])
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
      buildWarehouseSummaryFromDetail(fallbackWarehouseDetails['db9a789f-9df8-45ff-a252-96d4319c2f12']),
      buildWarehouseSummaryFromDetail(fallbackWarehouseDetails['3f224c42-00a5-49a6-955c-c8114d0a6b81']),
    ],
    routes: [
      buildRouteSummaryFromDetail(fallbackRouteDetails['5024fa82-f658-46c8-88bf-aece07d56f09']),
      buildRouteSummaryFromDetail(fallbackRouteDetails['528c1588-40fd-451b-8c86-2caa625602de']),
      buildRouteSummaryFromDetail(fallbackRouteDetails['9f91e82e-226a-48f7-a94c-907b79431739']),
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

export function buildFallbackWarehouseDetail(warehouseId: string): WarehouseDetailView {
  return fallbackWarehouseDetails[warehouseId]
    ?? fallbackWarehouseDetails['db9a789f-9df8-45ff-a252-96d4319c2f12']
}

export function buildFallbackRouteDetail(routeId: string): RouteDetailView {
  return fallbackRouteDetails[routeId]
    ?? fallbackRouteDetails['5024fa82-f658-46c8-88bf-aece07d56f09']
}

export function buildFallbackTransportSyncStatus(): TransportSyncStatusView {
  return {
    providerId: 'rest-transport-adapter',
    source: 'demo-fallback',
    liveImport: false,
    hasPersistedSnapshot: false,
    importedRouteCount: 0,
    importedRouteIds: [],
    importedRouteReferences: [],
    lastImportedAtUtc: null,
    lastSuccessfulSyncAtUtc: null,
    lastAttemptedSyncAtUtc: null,
    lastImportedAtLabel: null,
    lastActivityLabel: 'Last attempt 2026-06-20 10:15 UTC',
    syncStatus: 'degraded-live-snapshot',
    syncStatusLabel: 'Degraded Live Snapshot',
    syncDetail: 'Demo transport projection is available while upstream configuration remains partial.',
    healthStatus: 'Degraded',
    healthSummary: 'REST transport remains in demo fallback until a valid upstream endpoint and credentials are available.',
  }
}

export function buildFallbackRouteOptimization(detail: RouteDetailView): RouteOptimizationView {
  const pendingStops = pendingRouteStops(detail)
  const currentOrder = pendingStops.map((stop) => stop.name)
  const priorityOrder = [...pendingStops]
    .sort((left, right) => right.priority - left.priority || parseTimeWindowEndMinutes(left.timeWindowLabel) - parseTimeWindowEndMinutes(right.timeWindowLabel))
    .map((stop) => stop.name)
  const conservativeOrder = [...pendingStops]
    .sort((left, right) => parseTimeWindowEndMinutes(left.timeWindowLabel) - parseTimeWindowEndMinutes(right.timeWindowLabel) || right.priority - left.priority)
    .map((stop) => stop.name)

  const alternatives: RouteOptimizationAlternativeView[] = []
  if (priorityOrder.join('|') !== currentOrder.join('|')) {
    alternatives.push({
      label: 'priority-plan',
      orderedStopReferences: priorityOrder,
      objectiveScore: 92.4,
      summary: 'Higher-priority stops are moved earlier to protect the most sensitive deliveries.',
    })
  }

  if (conservativeOrder.join('|') !== currentOrder.join('|') && conservativeOrder.join('|') !== priorityOrder.join('|')) {
    alternatives.push({
      label: 'conservative-plan',
      orderedStopReferences: conservativeOrder,
      objectiveScore: 96.8,
      summary: 'Earlier-closing windows are visited sooner to reduce lateness risk.',
    })
  }

  return {
    routeId: detail.routeId,
    routeReference: detail.reference,
    status: 'optimized',
    objectiveScore: pendingStops.length === 0 ? null : Number((pendingStops.length * 18.5).toFixed(1)),
    orderedStopReferences: currentOrder,
    etaMinutes: Object.fromEntries(pendingStops.map((stop, index) => [stop.name, 660 + (index * 35)])),
    loadDistribution: Object.fromEntries(pendingStops.map((stop) => [stop.name, Math.max(1, Math.round(detail.totalLoadKilograms / Math.max(pendingStops.length, 1) / 10))])),
    constraintViolations: [],
    explanation: {
      selectedVehicleReason: `Vehicle ${detail.truckReference} remains assigned because the fallback keeps the current route posture.`,
      prioritizationReason: 'The local fallback preserves the current remaining stop order and exposes comparison plans for review.',
      tightConstraints: pendingStops.length >= 4 ? ['route duration'] : [],
      infeasibilityReason: null,
      tradeOffs: [
        'The optimization service was unavailable, so no solver-backed resequencing was attempted.',
        'Alternative plans are still shown so the dispatcher can compare risk posture manually.',
      ],
    },
    alternatives,
    solverBackend: 'frontend-local-fallback',
  }
}

export function mapApiWarehouseDetailToView(apiWarehouse: ApiWarehouseDetail): WarehouseDetailView {
  return {
    warehouseId: apiWarehouse.warehouseId,
    name: apiWarehouse.name,
    zoneCount: apiWarehouse.zoneCount,
    rackCount: apiWarehouse.rackCount,
    slotCount: apiWarehouse.slotCount,
    occupiedDockCount: apiWarehouse.occupiedDockCount,
    storedPalletCount: apiWarehouse.storedPalletCount,
    updatedAtLabel: formatUtcLabel(apiWarehouse.updatedAtUtc),
    zones: apiWarehouse.zones.map((zone) => ({
      code: zone.code,
      name: zone.name,
      status: normalizeZoneStatus(zone.status),
      description: zone.description,
      utilization: zone.utilizationPercent,
      pallets: zone.palletCount,
      throughputLabel: zone.throughputLabel,
    })),
    docks: apiWarehouse.docks.map((dock) => ({
      code: dock.code,
      status: normalizeDockStatus(dock.status),
      activityLabel: dock.activityLabel,
    })),
  }
}

export function mapApiRouteDetailToView(apiRoute: ApiRouteDetail): RouteDetailView {
  return {
    routeId: apiRoute.routeId,
    reference: apiRoute.reference,
    truckId: apiRoute.truckId,
    truckReference: apiRoute.truckReference,
    driverName: apiRoute.driverName,
    status: normalizeRouteStatus(apiRoute.status),
    truckStatus: apiRoute.truckStatus,
    truckCapacityKilograms: apiRoute.truckCapacityKilograms,
    totalLoadKilograms: apiRoute.totalLoadKilograms,
    stopCount: apiRoute.stopCount,
    shipmentCount: apiRoute.shipmentCount,
    completedDeliveryCount: apiRoute.completedDeliveryCount,
    updatedAtLabel: formatUtcLabel(apiRoute.updatedAtUtc),
    stops: apiRoute.stops.map((stop) => ({
      sequence: stop.sequence,
      name: stop.name,
      coordinateLabel: stop.coordinateLabel,
      timeWindowLabel: stop.timeWindowLabel,
    })),
    shipments: apiRoute.shipments.map((shipment) => ({
      reference: shipment.reference,
      status: shipment.status as RouteShipmentView['status'],
      loadWeightKilograms: shipment.loadWeightKilograms,
      orderReference: shipment.orderReference,
    })),
    deliveries: apiRoute.deliveries.map((delivery) => ({
      reference: delivery.reference,
      stopSequence: delivery.stopSequence,
      stopName: delivery.stopName,
      shipmentReference: delivery.shipmentReference,
      status: delivery.status as RouteDeliveryView['status'],
    })),
  }
}

export function mapApiTransportSyncStatusToView(apiStatus: ApiTransportSyncStatus): TransportSyncStatusView {
  return {
    providerId: apiStatus.providerId,
    source: normalizeTransportSyncSource(apiStatus.source),
    liveImport: apiStatus.liveImport,
    hasPersistedSnapshot: apiStatus.hasPersistedSnapshot,
    importedRouteCount: apiStatus.importedRouteCount,
    importedRouteIds: apiStatus.importedRouteIds,
    importedRouteReferences: apiStatus.importedRouteReferences,
    lastImportedAtUtc: apiStatus.lastImportedAtUtc,
    lastSuccessfulSyncAtUtc: apiStatus.lastSuccessfulSyncAt,
    lastAttemptedSyncAtUtc: apiStatus.lastAttemptedSyncAt,
    lastImportedAtLabel: apiStatus.lastImportedAtUtc ? formatUtcLabel(apiStatus.lastImportedAtUtc) : null,
    lastActivityLabel: formatRelativeSyncLabel(apiStatus.lastSuccessfulSyncAt, apiStatus.lastAttemptedSyncAt),
    syncStatus: apiStatus.syncStatus,
    syncStatusLabel: formatSyncStatusLabel(apiStatus.syncStatus),
    syncDetail: apiStatus.syncDetail,
    healthStatus: normalizeProviderHealthStatus(apiStatus.health.status),
    healthSummary: apiStatus.health.summary,
  }
}

export function mapApiTransportSyncDiffToView(apiDiff: ApiTransportSyncDiff): TransportSyncDiffView {
  const normalizeChangeType = (value: string): TransportSyncRouteDiffItemView['changeType'] => {
    if (value === 'Added' || value === 'Removed' || value === 'Changed' || value === 'Unchanged') {
      return value
    }

    return 'Changed'
  }

  return {
    hasComparableHistory: apiDiff.hasComparableHistory,
    detail: apiDiff.detail,
    latestImportedAtLabel: apiDiff.latestImportedAtUtc ? formatUtcLabel(apiDiff.latestImportedAtUtc) : null,
    previousImportedAtLabel: apiDiff.previousImportedAtUtc ? formatUtcLabel(apiDiff.previousImportedAtUtc) : null,
    latestRouteCount: apiDiff.latestRouteCount,
    previousRouteCount: apiDiff.previousRouteCount,
    addedRouteCount: apiDiff.addedRouteCount,
    removedRouteCount: apiDiff.removedRouteCount,
    changedRouteCount: apiDiff.changedRouteCount,
    routeDiffs: apiDiff.routeDiffs.map((item) => ({
      routeReference: item.routeReference,
      changeType: normalizeChangeType(item.changeType),
      previousStatus: item.previousStatus,
      currentStatus: item.currentStatus,
      previousStopCount: item.previousStopCount,
      currentStopCount: item.currentStopCount,
      previousShipmentCount: item.previousShipmentCount,
      currentShipmentCount: item.currentShipmentCount,
      previousCompletedDeliveryCount: item.previousCompletedDeliveryCount,
      currentCompletedDeliveryCount: item.currentCompletedDeliveryCount,
      summary: item.summary,
    })),
  }
}

export function buildFallbackTransportSyncDiff(): TransportSyncDiffView {
  return {
    hasComparableHistory: false,
    detail: 'Transport sync diff is unavailable in fallback mode.',
    latestImportedAtLabel: null,
    previousImportedAtLabel: null,
    latestRouteCount: 0,
    previousRouteCount: 0,
    addedRouteCount: 0,
    removedRouteCount: 0,
    changedRouteCount: 0,
    routeDiffs: [],
  }
}

export function buildFallbackTransportSyncHistory(): TransportSyncHistoryView {
  return {
    count: 1,
    summary: 'Transport sync history is unavailable in fallback mode.',
    entries: [
      {
        id: 'fallback-sync-history-1',
        createdAtLabel: 'Local fallback',
        importedAtLabel: null,
        source: 'fallback',
        status: 'unavailable',
        importedRouteCount: 0,
        healthStatus: 'Degraded',
        summary: 'Recent import history is unavailable, so only the current transport snapshot can be inspected.',
        hasComparablePrevious: false,
        addedRouteCount: 0,
        removedRouteCount: 0,
        changedRouteCount: 0,
        routeReferencePreview: [],
      },
    ],
  }
}

type ApiTransportSyncHistoryEntry = {
  runId: number
  providerId: string
  source: string
  status: string
  createdAtUtc: string
  importedAtUtc: string | null
  importedRouteCount: number
  importedRouteReferences: string[]
  healthStatus: string
  summary: string
  hasComparablePrevious: boolean
  addedRouteCount: number
  removedRouteCount: number
  changedRouteCount: number
}

type ApiTransportSyncHistory = {
  count: number
  summary: string
  entries: ApiTransportSyncHistoryEntry[]
}

type ApiTransportExceptionWorkbenchItem = {
  exceptionId: string
  severity: string
  category: string
  title: string
  detail: string
  routeId: string | null
  routeReference: string | null
  recommendedAction: string
  actionLabel: string
  resolutionStatus: string | null
  resolutionNote: string | null
  resolutionUpdatedAtUtc: string | null
  evidence: string[]
}

type ApiTransportExceptionWorkbench = {
  generatedAtUtc: string
  exceptionCount: number
  summary: string
  groups: Array<{
    groupKey: string
    label: string
    highestSeverity: string
    count: number
    summary: string
    recommendedAction: string
    actionLabel: string
  }>
  items: ApiTransportExceptionWorkbenchItem[]
}

type ApiTransportExceptionResolutionHistoryEntry = {
  historyEntryId: string
  exceptionId: string
  status: string
  note: string | null
  updatedAtUtc: string
}

type ApiTransportExceptionResolutionHistory = {
  updatedAtUtc: string
  entryCount: number
  entries: ApiTransportExceptionResolutionHistoryEntry[]
}

export function mapApiTransportSyncHistoryToView(apiHistory: ApiTransportSyncHistory): TransportSyncHistoryView {
  return {
    count: apiHistory.count,
    summary: apiHistory.summary,
    entries: apiHistory.entries.map((entry) => ({
      id: String(entry.runId),
      createdAtLabel: formatUtcLabel(entry.createdAtUtc),
      importedAtLabel: entry.importedAtUtc ? formatUtcLabel(entry.importedAtUtc) : null,
      source: entry.source,
      status: entry.status,
      importedRouteCount: entry.importedRouteCount,
      healthStatus: normalizeProviderHealthStatus(entry.healthStatus),
      summary: entry.summary,
      hasComparablePrevious: entry.hasComparablePrevious,
      addedRouteCount: entry.addedRouteCount,
      removedRouteCount: entry.removedRouteCount,
      changedRouteCount: entry.changedRouteCount,
      routeReferencePreview: entry.importedRouteReferences.slice(0, 3),
    })),
  }
}

export function buildFallbackTransportExceptionWorkbench(): TransportExceptionWorkbenchView {
  return {
    generatedAtLabel: 'Local fallback',
    exceptionCount: 1,
    summary: 'Transport exception workbench is unavailable in fallback mode.',
    groups: [
      {
        groupKey: 'fallback',
        label: 'Fallback',
        highestSeverity: 'Warning',
        count: 1,
        summary: 'Fallback exception posture is active.',
        recommendedAction: 'sync-import',
        actionLabel: 'Import snapshot',
      },
    ],
    items: [
      {
        id: 'fallback-transport-exception',
        severity: 'Warning',
        category: 'Fallback',
        title: 'Transport exceptions are not available yet',
        detail: 'The workbench needs live transport sync, diff, and route evidence before it can prioritize operator exceptions.',
        routeId: null,
        routeReference: null,
        recommendedAction: 'sync-import',
        actionLabel: 'Import snapshot',
        resolutionStatus: null,
        resolutionNote: null,
        resolutionUpdatedAtLabel: null,
        evidence: ['Fallback data is active', 'No dedicated exception projection is available'],
      },
    ],
  }
}

export function buildFallbackTransportExceptionResolutionHistory(): TransportExceptionResolutionHistoryView {
  return {
    count: 0,
    summary: 'Resolution history is unavailable in fallback mode.',
    entries: [],
  }
}

function normalizeTransportExceptionSeverity(status: string): TransportExceptionWorkbenchItemView['severity'] {
  if (status === 'Critical' || status === 'Warning') {
    return status
  }

  return 'Info'
}

function normalizeTransportExceptionAction(action: string): TransportExceptionWorkbenchItemView['recommendedAction'] {
  switch (action) {
    case 'sync-import':
    case 'sync-refresh':
    case 'focus-route':
    case 'focus-route-diff':
    case 'optimization-refresh':
    case 'selected-diff':
    case 'review-history':
      return action
    default:
      return 'focus-route'
  }
}

function normalizeTransportExceptionResolutionStatus(
  status: string | null
): TransportExceptionWorkbenchItemView['resolutionStatus'] {
  if (status === 'Reviewed' || status === 'Resolved' || status === 'Deferred') {
    return status
  }

  return null
}

function normalizeTransportExceptionHistoryStatus(
  status: string
): TransportExceptionResolutionHistoryEntryView['status'] {
  if (status === 'Resolved' || status === 'Deferred') {
    return status
  }

  return 'Reviewed'
}

export function mapApiTransportExceptionWorkbenchToView(apiWorkbench: ApiTransportExceptionWorkbench): TransportExceptionWorkbenchView {
  return {
    generatedAtLabel: formatUtcLabel(apiWorkbench.generatedAtUtc),
    exceptionCount: apiWorkbench.exceptionCount,
    summary: apiWorkbench.summary,
    groups: apiWorkbench.groups.map((group) => ({
      groupKey: group.groupKey,
      label: group.label,
      highestSeverity: normalizeTransportExceptionSeverity(group.highestSeverity),
      count: group.count,
      summary: group.summary,
      recommendedAction: normalizeTransportExceptionAction(group.recommendedAction),
      actionLabel: group.actionLabel,
    })),
    items: apiWorkbench.items.map((item) => ({
      id: item.exceptionId,
      severity: normalizeTransportExceptionSeverity(item.severity),
      category: item.category,
      title: item.title,
      detail: item.detail,
      routeId: item.routeId,
      routeReference: item.routeReference,
      recommendedAction: normalizeTransportExceptionAction(item.recommendedAction),
      actionLabel: item.actionLabel,
      resolutionStatus: normalizeTransportExceptionResolutionStatus(item.resolutionStatus),
      resolutionNote: item.resolutionNote,
      resolutionUpdatedAtLabel: item.resolutionUpdatedAtUtc ? formatUtcLabel(item.resolutionUpdatedAtUtc) : null,
      evidence: item.evidence,
    })),
  }
}

export function mapApiTransportExceptionResolutionHistoryToView(
  apiHistory: ApiTransportExceptionResolutionHistory
): TransportExceptionResolutionHistoryView {
  const entries = apiHistory.entries.map((entry) => ({
    id: entry.historyEntryId,
    exceptionId: entry.exceptionId,
    status: normalizeTransportExceptionHistoryStatus(entry.status),
    note: entry.note,
    updatedAtLabel: formatUtcLabel(entry.updatedAtUtc),
  }))

  return {
    count: apiHistory.entryCount,
    summary:
      entries.length === 0
        ? 'No persisted exception resolution history is available yet.'
        : `${entries.length} recent resolution update(s) are available for operator review.`,
    entries,
  }
}

export function mapApiRouteOptimizationToView(apiEnvelope: ApiRouteOptimizationEnvelope): { optimization: RouteOptimizationView; source: 'api' | 'fallback'; errorMessage: string | null } {
  return {
    optimization: {
      routeId: apiEnvelope.optimization.routeId,
      routeReference: apiEnvelope.optimization.routeReference,
      status: apiEnvelope.optimization.status === 'infeasible' ? 'infeasible' : 'optimized',
      objectiveScore: apiEnvelope.optimization.objectiveScore,
      orderedStopReferences: apiEnvelope.optimization.orderedStopReferences,
      etaMinutes: apiEnvelope.optimization.etaMinutes,
      loadDistribution: apiEnvelope.optimization.loadDistribution,
      constraintViolations: apiEnvelope.optimization.constraintViolations,
      explanation: {
        selectedVehicleReason: apiEnvelope.optimization.explanation.selectedVehicleReason,
        prioritizationReason: apiEnvelope.optimization.explanation.prioritizationReason,
        tightConstraints: apiEnvelope.optimization.explanation.tightConstraints,
        infeasibilityReason: apiEnvelope.optimization.explanation.infeasibilityReason,
        tradeOffs: apiEnvelope.optimization.explanation.tradeOffs,
      },
      alternatives: apiEnvelope.optimization.alternatives.map((alternative) => ({
        label: alternative.label,
        orderedStopReferences: alternative.orderedStopReferences,
        objectiveScore: alternative.objectiveScore,
        summary: alternative.summary,
      })),
      solverBackend: apiEnvelope.optimization.solverBackend,
    },
    source: apiEnvelope.source === 'api' ? 'api' : 'fallback',
    errorMessage: apiEnvelope.errorMessage,
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
        configurationReadiness: 'Configured',
        configurationEnvironment: 'local-demo',
        configurationEnabled: true,
        configuredFields: ['sourcePath', 'importSchedule'],
        missingFields: [],
        editableSettings: [
          { key: 'sourcePath', value: 'data/imports/warehouse-demo.csv', required: true },
          { key: 'importSchedule', value: 'manual', required: true },
        ],
        authMode: 'none',
        authConfigured: true,
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
        configurationReadiness: 'Auth Key Missing',
        configurationEnvironment: 'sandbox',
        configurationEnabled: true,
        configuredFields: ['baseUrl', 'authMode'],
        missingFields: [],
        editableSettings: [
          { key: 'baseUrl', value: 'https://sandbox.example.invalid/transport', required: true },
          { key: 'authMode', value: 'api-key', required: true },
        ],
        authMode: 'api-key',
        authConfigured: false,
        syncStatusLabel: 'Awaiting Configuration',
        lastActivityLabel: 'Last attempt 2026-06-20 10:15 UTC',
        summary: 'REST adapter skeleton is available but not yet configured against a live upstream service.',
        capabilities: ['Read', 'Write', 'Commands', 'History', 'Read-only'],
        supportedReadModels: ['RouteSummaryReadModel', 'RouteDetailReadModel'],
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
        configurationReadiness: 'Configured',
        configurationEnvironment: 'local-demo',
        configurationEnabled: true,
        configuredFields: ['streamTopic', 'snapshotIntervalSeconds'],
        missingFields: [],
        editableSettings: [
          { key: 'streamTopic', value: 'fleet/gps/demo', required: true },
          { key: 'snapshotIntervalSeconds', value: '15', required: true },
        ],
        authMode: 'none',
        authConfigured: true,
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
      configurationReadiness: provider.configuration.readiness,
      configurationEnvironment: provider.configuration.environment,
      configurationEnabled: provider.configuration.enabled,
      configuredFields: provider.configuration.configuredFields,
      missingFields: provider.configuration.missingFields,
      editableSettings: provider.configuration.settings.map((setting) => ({
        key: setting.key,
        value: setting.value,
        required: setting.required,
      })),
      authMode: provider.configuration.authMode ?? 'none',
      authConfigured: provider.configuration.authConfigured ?? true,
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
