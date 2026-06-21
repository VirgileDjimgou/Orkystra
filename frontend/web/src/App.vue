<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import WarehouseTwinScene from './components/WarehouseTwinScene.vue'
import {
  buildFallbackOverview,
  buildFallbackWarehouseDetail,
  buildFallbackProviderCatalog,
  simulationSpeeds,
  type ControlTowerOverviewView,
  type WarehouseDetailView,
  type ProviderCatalogView,
  type ProviderCatalogItemView,
} from './data/controlTower'
import { loadControlTowerOverview } from './services/controlTowerApi'
import { loadProviderCatalog, updateProviderConfiguration } from './services/providerCatalogApi'
import { loadWarehouseProjection } from './services/warehouseApi'

type DataConnectionState = 'loading' | 'api' | 'fallback' | 'stale'

const navigationItems = [
  { label: 'Control Tower', short: 'CT', active: true },
  { label: 'Warehouse', short: 'WH', active: false },
  { label: 'Transport', short: 'TR', active: false },
  { label: 'Scenarios', short: 'SC', active: false },
]

const fallbackOverview = buildFallbackOverview()
const overview = ref<ControlTowerOverviewView>(fallbackOverview)
const warehouseDetail = ref<WarehouseDetailView>(buildFallbackWarehouseDetail(fallbackOverview.warehouses[0].warehouseId))
const isApiConnected = ref(false)
const isRefreshingWorkspace = ref(false)
const loadErrorMessage = ref<string | null>(null)
const overviewConnectionState = ref<DataConnectionState>('loading')
const warehouseDetailErrorMessage = ref<string | null>(null)
const warehouseConnectionState = ref<DataConnectionState>('loading')
const providerCatalog = ref<ProviderCatalogView>(buildFallbackProviderCatalog())
const isProviderCatalogConnected = ref(false)
const providerCatalogErrorMessage = ref<string | null>(null)
const providerCatalogConnectionState = ref<DataConnectionState>('loading')
const savingProviderId = ref<string | null>(null)
const providerConfigurationNotice = ref<Record<string, string>>({})
const providerConfigurationDrafts = ref<Record<string, { enabled: boolean; environment: string; settings: Record<string, string> }>>({})
const selectedScenarioId = ref(fallbackOverview.scenarios[0].scenarioId)
const selectedWarehouseId = ref(fallbackOverview.warehouses[0].warehouseId)
const simulationState = ref<'Running' | 'Paused'>('Running')
const selectedSpeed = ref(simulationSpeeds[1])
let connectionRecoveryHandle = 0

const currentScenario = computed(
  () => overview.value.scenarios.find((scenario) => scenario.scenarioId === selectedScenarioId.value) ?? overview.value.scenarios[0],
)

const currentWarehouse = computed(
  () => overview.value.warehouses.find((warehouse) => warehouse.warehouseId === selectedWarehouseId.value) ?? overview.value.warehouses[0],
)

const currentRiskCount = computed(() => overview.value.alerts.filter((alert) => alert.severity !== 'Info').length)
const delayedRouteCount = computed(() => overview.value.routes.filter((route) => route.status !== 'On time').length)
const degradedProviderCount = computed(() => overview.value.providers.filter((provider) => provider.healthStatus !== 'Healthy').length)
const warehouseUtilization = computed(() => {
  const warehouse = currentWarehouse.value
  return Math.round((warehouse.storedPalletCount / warehouse.slotCount) * 100)
})

function toneForConnectionState(state: DataConnectionState): 'is-loading' | 'is-running' | 'is-paused' | 'is-warning' {
  if (state === 'loading') {
    return 'is-loading'
  }

  if (state === 'api') {
    return 'is-running'
  }

  if (state === 'stale') {
    return 'is-warning'
  }

  return 'is-paused'
}

function labelForConnectionState(state: DataConnectionState, apiLabel: string, fallbackLabel: string): string {
  if (state === 'loading') {
    return 'Loading'
  }

  if (state === 'api') {
    return apiLabel
  }

  if (state === 'stale') {
    return `Stale ${apiLabel}`
  }

  return fallbackLabel
}

const scenarioStatusTone = computed(() => (simulationState.value === 'Running' ? 'is-running' : 'is-paused'))
const providerEditorTone = computed(() => toneForConnectionState(providerCatalogConnectionState.value))
const dataConnectionTone = computed(() => toneForConnectionState(overviewConnectionState.value))
const warehouseProjectionTone = computed(() => toneForConnectionState(warehouseConnectionState.value))
const overviewConnectionLabel = computed(() => labelForConnectionState(overviewConnectionState.value, 'API live', 'Fallback active'))
const warehouseProjectionLabel = computed(() => labelForConnectionState(warehouseConnectionState.value, 'Warehouse API live', 'Warehouse fallback'))
const catalogConnectionLabel = computed(() => labelForConnectionState(providerCatalogConnectionState.value, 'Editable local API', 'Read-only fallback'))
const tenantSummary = computed(() => {
  if (overviewConnectionState.value === 'api') {
    return 'API-backed control tower'
  }

  if (overviewConnectionState.value === 'stale') {
    return 'Last successful API snapshot retained'
  }

  if (overviewConnectionState.value === 'loading') {
    return 'Connecting to local services'
  }

  return 'Fallback demo data active'
})
const connectionMessage = computed(() => {
  return loadErrorMessage.value ?? warehouseDetailErrorMessage.value ?? providerCatalogErrorMessage.value ?? 'All local data surfaces are ready.'
})

function toggleSimulation(): void {
  simulationState.value = simulationState.value === 'Running' ? 'Paused' : 'Running'
}

function setSimulationSpeed(speed: (typeof simulationSpeeds)[number]): void {
  selectedSpeed.value = speed
}

function syncProviderDrafts(catalog: ProviderCatalogView): void {
  providerConfigurationDrafts.value = Object.fromEntries(
    catalog.providers.map((provider) => [
      provider.providerId,
      {
        enabled: provider.configurationEnabled,
        environment: provider.configurationEnvironment,
        settings: Object.fromEntries(provider.editableSettings.map((setting) => [setting.key, setting.value])),
      },
    ]),
  )
}

function providerDraft(providerId: string): { enabled: boolean; environment: string; settings: Record<string, string> } {
  const existing = providerConfigurationDrafts.value[providerId]

  if (existing) {
    return existing
  }

  providerConfigurationDrafts.value[providerId] = {
    enabled: true,
    environment: 'local-demo',
    settings: {},
  }

  return providerConfigurationDrafts.value[providerId]
}

function applyOverviewResult(result: Awaited<ReturnType<typeof loadControlTowerOverview>>, preserveExisting: boolean): void {
  const keepCurrentSnapshot = preserveExisting &&
    result.source === 'fallback' &&
    (overviewConnectionState.value === 'api' || overviewConnectionState.value === 'stale')

  if (!keepCurrentSnapshot) {
    overview.value = result.overview
    selectedScenarioId.value = result.overview.scenarios.some((scenario) => scenario.scenarioId === selectedScenarioId.value)
      ? selectedScenarioId.value
      : (result.overview.scenarios[0]?.scenarioId ?? fallbackOverview.scenarios[0].scenarioId)
    selectedWarehouseId.value = result.overview.warehouses.some((warehouse) => warehouse.warehouseId === selectedWarehouseId.value)
      ? selectedWarehouseId.value
      : (result.overview.warehouses[0]?.warehouseId ?? fallbackOverview.warehouses[0].warehouseId)
  }

  overviewConnectionState.value = keepCurrentSnapshot
    ? 'stale'
    : result.source === 'api'
      ? 'api'
      : 'fallback'
  isApiConnected.value = overviewConnectionState.value !== 'fallback'
  loadErrorMessage.value = keepCurrentSnapshot && result.errorMessage
    ? `${result.errorMessage} Showing the last successful API snapshot.`
    : result.errorMessage
}

function applyCatalogResult(result: Awaited<ReturnType<typeof loadProviderCatalog>>, preserveExisting: boolean): void {
  const keepCurrentSnapshot = preserveExisting &&
    result.source === 'fallback' &&
    (providerCatalogConnectionState.value === 'api' || providerCatalogConnectionState.value === 'stale')

  if (!keepCurrentSnapshot) {
    providerCatalog.value = result.catalog
    syncProviderDrafts(result.catalog)
  }

  providerCatalogConnectionState.value = keepCurrentSnapshot
    ? 'stale'
    : result.source === 'api'
      ? 'api'
      : 'fallback'
  isProviderCatalogConnected.value = providerCatalogConnectionState.value !== 'fallback'
  providerCatalogErrorMessage.value = keepCurrentSnapshot && result.errorMessage
    ? `${result.errorMessage} Keeping the last successful editable catalog.`
    : result.errorMessage
}

function applyWarehouseProjectionResult(
  result: Awaited<ReturnType<typeof loadWarehouseProjection>>,
  preserveExisting: boolean,
  requestedWarehouseId: string,
): void {
  const keepCurrentSnapshot = preserveExisting &&
    result.source === 'fallback' &&
    warehouseDetail.value.warehouseId === requestedWarehouseId &&
    (warehouseConnectionState.value === 'api' || warehouseConnectionState.value === 'stale')

  if (!keepCurrentSnapshot) {
    warehouseDetail.value = result.warehouse
  }

  warehouseConnectionState.value = keepCurrentSnapshot
    ? 'stale'
    : result.source === 'api'
      ? 'api'
      : 'fallback'
  warehouseDetailErrorMessage.value = keepCurrentSnapshot && result.errorMessage
    ? `${result.errorMessage} Keeping the last successful warehouse projection.`
    : result.errorMessage
}

async function refreshWarehouseProjection(warehouseId: string, preserveExisting = true): Promise<void> {
  if (!preserveExisting) {
    warehouseConnectionState.value = 'loading'
  }

  const result = await loadWarehouseProjection(warehouseId)
  applyWarehouseProjectionResult(result, preserveExisting, warehouseId)
}

async function refreshWorkspace(preserveExisting = true): Promise<void> {
  isRefreshingWorkspace.value = true

  if (!preserveExisting) {
    overviewConnectionState.value = 'loading'
    warehouseConnectionState.value = 'loading'
    providerCatalogConnectionState.value = 'loading'
  }

  const [overviewResult, catalogResult] = await Promise.all([loadControlTowerOverview(), loadProviderCatalog()])
  applyOverviewResult(overviewResult, preserveExisting)
  applyCatalogResult(catalogResult, preserveExisting)
  await refreshWarehouseProjection(selectedWarehouseId.value, preserveExisting)
  isRefreshingWorkspace.value = false

  const hasPartialFallback =
    [overviewConnectionState.value, warehouseConnectionState.value, providerCatalogConnectionState.value].some((state) => state === 'fallback') &&
    [overviewConnectionState.value, warehouseConnectionState.value, providerCatalogConnectionState.value].some((state) => state === 'api' || state === 'stale')

  if (hasPartialFallback) {
    window.clearTimeout(connectionRecoveryHandle)
    connectionRecoveryHandle = window.setTimeout(() => {
      void refreshWorkspace(true)
    }, 1500)
  }
}

async function refreshProviderCatalog(preserveExisting = true): Promise<void> {
  if (!preserveExisting) {
    providerCatalogConnectionState.value = 'loading'
  }

  const catalogResult = await loadProviderCatalog()
  applyCatalogResult(catalogResult, preserveExisting)
}

async function saveProviderConfiguration(provider: ProviderCatalogItemView): Promise<void> {
  const draft = providerDraft(provider.providerId)
  providerConfigurationNotice.value = {
    ...providerConfigurationNotice.value,
    [provider.providerId]: '',
  }

  savingProviderId.value = provider.providerId

  try {
    await updateProviderConfiguration({
      providerId: provider.providerId,
      enabled: draft.enabled,
      environment: draft.environment,
      settings: draft.settings,
    })

    await refreshProviderCatalog(true)
    providerConfigurationNotice.value = {
      ...providerConfigurationNotice.value,
      [provider.providerId]: 'Saved locally.',
    }
  } catch (error) {
    providerConfigurationNotice.value = {
      ...providerConfigurationNotice.value,
      [provider.providerId]: error instanceof Error ? error.message : 'Unable to save provider configuration.',
    }
  } finally {
    savingProviderId.value = null
  }
}

onMounted(async () => {
  await refreshWorkspace(false)
})

watch(selectedWarehouseId, (nextWarehouseId, previousWarehouseId) => {
  if (nextWarehouseId === previousWarehouseId || isRefreshingWorkspace.value) {
    return
  }

  void refreshWarehouseProjection(nextWarehouseId, false)
})

onBeforeUnmount(() => {
  window.clearTimeout(connectionRecoveryHandle)
})
</script>

<template>
  <main class="app-shell">
    <aside class="sidebar" aria-label="Primary navigation">
      <div class="brand">
        <div class="brand-mark">O</div>
        <div>
          <strong>Orkystra</strong>
          <p>Smart Logistics Twin</p>
        </div>
      </div>

      <nav class="nav-list">
        <a
          v-for="item in navigationItems"
          :key="item.label"
          href="#"
          class="nav-item"
          :class="{ 'is-active': item.active }"
        >
          <span class="nav-icon">{{ item.short }}</span>
          <span>{{ item.label }}</span>
        </a>
      </nav>

      <section class="sidebar-panel">
        <span class="panel-label">Tenant</span>
        <strong>{{ overview.tenantId }}</strong>
        <p>{{ tenantSummary }}</p>
        <span class="status-pill" :class="dataConnectionTone">
          {{ overviewConnectionLabel }}
        </span>
      </section>

      <section class="sidebar-panel">
        <span class="panel-label">Pipeline</span>
        <ul class="sidebar-list">
          <li>Warehouse summary projection ready</li>
          <li>Route summary projection ready</li>
          <li>Control tower overview served by API</li>
        </ul>
      </section>
    </aside>

    <section class="workspace">
      <header class="topbar">
        <div class="headline">
          <p class="eyebrow">Unified warehouse and transport supervision</p>
          <h1>Control Tower</h1>
          <span class="status-pill" :class="scenarioStatusTone">
            {{ simulationState }} - {{ selectedSpeed.label }}
          </span>
        </div>

        <div class="topbar-actions">
          <button type="button" class="refresh-button" :disabled="isRefreshingWorkspace" @click="refreshWorkspace(true)">
            {{ isRefreshingWorkspace ? 'Refreshing...' : 'Refresh data' }}
          </button>

          <label class="scenario-select">
            <span>Scenario</span>
            <select v-model="selectedScenarioId">
              <option v-for="scenario in overview.scenarios" :key="scenario.scenarioId" :value="scenario.scenarioId">
                {{ scenario.name }}
              </option>
            </select>
          </label>

          <div class="control-group" aria-label="Simulation controls">
            <button type="button" @click="toggleSimulation">
              {{ simulationState === 'Running' ? 'Pause' : 'Resume' }}
            </button>
            <button
              v-for="speed in simulationSpeeds"
              :key="speed.label"
              type="button"
              class="ghost-button"
              :class="{ 'is-selected': selectedSpeed.label === speed.label }"
              @click="setSimulationSpeed(speed)"
            >
              {{ speed.label }}
            </button>
          </div>
        </div>
      </header>

      <section class="summary-strip" aria-label="Operational summary">
        <article class="metric-panel">
          <span class="panel-label">Scenario clock</span>
          <strong>{{ currentScenario.currentTimeLabel }}</strong>
          <p>Seed {{ currentScenario.seed }} - {{ currentScenario.injectedEventCount }} injected events</p>
        </article>

        <article class="metric-panel">
          <span class="panel-label">Warehouse occupancy</span>
          <strong>{{ warehouseUtilization }}%</strong>
          <p>{{ currentWarehouse.storedPalletCount }} pallets across {{ currentWarehouse.slotCount }} slots</p>
        </article>

        <article class="metric-panel">
          <span class="panel-label">Transport watch</span>
          <strong>{{ delayedRouteCount }} routes off plan</strong>
          <p>{{ overview.routes.length }} active routes on the ETA board</p>
        </article>

        <article class="metric-panel">
          <span class="panel-label">Operational risk</span>
          <strong>{{ currentRiskCount }} active signals</strong>
          <p>{{ overview.alerts[0]?.title ?? 'No active alert' }}</p>
        </article>

        <article class="metric-panel">
          <span class="panel-label">Provider health</span>
          <strong>{{ degradedProviderCount }} degraded links</strong>
          <p>Overview generated {{ overview.generatedAtLabel }}</p>
        </article>
      </section>

      <section class="surface connection-surface">
        <div class="surface-heading">
          <div>
            <span class="panel-label">Connection state</span>
            <h2>Data access posture</h2>
          </div>
          <span class="mini-badge">{{ isRefreshingWorkspace ? 'Refreshing' : 'Stable view' }}</span>
        </div>

        <div class="connection-grid">
          <article class="connection-card">
            <div class="connection-card-head">
              <div>
                <strong>Overview feed</strong>
                <p>Scenario, warehouse, transport, alerts, and provider watch.</p>
              </div>
              <span class="status-pill" :class="dataConnectionTone">{{ overviewConnectionLabel }}</span>
            </div>
            <p>{{ loadErrorMessage ?? 'Overview endpoint is serving the operator workspace.' }}</p>
          </article>

          <article class="connection-card">
            <div class="connection-card-head">
              <div>
                <strong>Warehouse projection</strong>
                <p>Zone posture, dock availability, and twin-facing warehouse detail.</p>
              </div>
              <span class="status-pill" :class="warehouseProjectionTone">{{ warehouseProjectionLabel }}</span>
            </div>
            <p>{{ warehouseDetailErrorMessage ?? `Detailed warehouse projection updated ${warehouseDetail.updatedAtLabel}.` }}</p>
          </article>

          <article class="connection-card">
            <div class="connection-card-head">
              <div>
                <strong>Provider catalog</strong>
                <p>Connector inventory, runtime posture, and local editing flow.</p>
              </div>
              <span class="status-pill" :class="providerEditorTone">{{ catalogConnectionLabel }}</span>
            </div>
            <p>{{ providerCatalogErrorMessage ?? 'Provider catalog is available for local inspection and editing.' }}</p>
          </article>
        </div>

        <p class="connection-summary">{{ connectionMessage }}</p>
      </section>

      <section class="workspace-grid">
        <section class="surface warehouse-surface">
          <div class="surface-heading">
            <div>
              <span class="panel-label">3D warehouse twin</span>
              <h2>{{ currentWarehouse.name }}</h2>
            </div>

            <div class="warehouse-actions">
              <span class="status-pill" :class="warehouseProjectionTone">{{ warehouseProjectionLabel }}</span>
              <label class="compact-select">
                <span>View</span>
                <select v-model="selectedWarehouseId">
                  <option v-for="warehouse in overview.warehouses" :key="warehouse.warehouseId" :value="warehouse.warehouseId">
                    {{ warehouse.name }}
                  </option>
                </select>
              </label>
            </div>
          </div>

          <div class="surface-body">
            <WarehouseTwinScene
              :zones="warehouseDetail.zones"
              :occupied-dock-count="warehouseDetail.occupiedDockCount"
              :stored-pallet-count="warehouseDetail.storedPalletCount"
            />

            <div class="warehouse-detail-grid">
              <div class="zone-strip" aria-label="Warehouse zones">
                <article v-for="zone in warehouseDetail.zones" :key="zone.code" class="zone-tile">
                  <div class="zone-head">
                    <div>
                      <strong>{{ zone.code }}</strong>
                      <p class="zone-label">{{ zone.name }}</p>
                    </div>
                    <span>{{ zone.status }}</span>
                  </div>
                  <p>{{ zone.description }}</p>
                  <div class="zone-meta">
                    <span>{{ zone.utilization }}% full</span>
                    <span>{{ zone.pallets }} pallets</span>
                    <span>{{ zone.throughputLabel }}</span>
                  </div>
                </article>
              </div>

              <div class="dock-strip" aria-label="Warehouse docks">
                <article v-for="dock in warehouseDetail.docks" :key="dock.code" class="dock-card">
                  <div class="zone-head">
                    <strong>{{ dock.code }}</strong>
                    <span class="dock-status" :class="dock.status === 'Occupied' ? 'is-occupied' : 'is-available'">
                      {{ dock.status }}
                    </span>
                  </div>
                  <p>{{ dock.activityLabel }}</p>
                </article>
                <p class="warehouse-summary-note">Detailed warehouse projection updated {{ warehouseDetail.updatedAtLabel }}.</p>
              </div>
            </div>
          </div>
        </section>

        <section class="surface transport-surface">
          <div class="surface-heading">
            <div>
              <span class="panel-label">Transport board</span>
              <h2>Route network</h2>
            </div>
            <span class="mini-badge">{{ isApiConnected ? 'API projection' : 'Fallback projection' }}</span>
          </div>

          <div class="map-stage" aria-label="Transport map placeholder">
            <div class="map-grid"></div>
            <div
              v-for="route in overview.routes"
              :key="route.routeId"
              class="route-marker"
              :class="route.status === 'Delayed' ? 'is-delayed' : route.status === 'At risk' ? 'is-risk' : 'is-normal'"
              :style="{ left: route.mapX, top: route.mapY }"
            >
              <strong>{{ route.reference }}</strong>
              <span>{{ route.truckReference }}</span>
            </div>
            <div class="map-legend">
              <span><i class="legend-swatch normal"></i> On time</span>
              <span><i class="legend-swatch risk"></i> At risk</span>
              <span><i class="legend-swatch delayed"></i> Delayed</span>
            </div>
          </div>

          <div class="route-table">
            <article v-for="route in overview.routes" :key="route.routeId" class="route-row">
              <div>
                <strong>{{ route.reference }}</strong>
                <p>{{ route.truckReference }} - {{ route.stopCount }} stops - {{ route.shipmentCount }} shipments</p>
              </div>
              <div class="route-meta">
                <span class="route-status">{{ route.status }}</span>
                <span>{{ route.nextEtaLabel }}</span>
              </div>
            </article>
          </div>
        </section>

        <section class="surface controls-surface">
          <div class="surface-heading">
            <div>
              <span class="panel-label">Scenario control panel</span>
              <h2>{{ currentScenario.name }}</h2>
            </div>
            <span class="mini-badge">{{ currentScenario.mode }}</span>
          </div>

          <div class="control-columns">
            <div class="timeline-panel">
              <div class="timeline-metric">
                <span>Current time</span>
                <strong>{{ currentScenario.currentTimeLabel }}</strong>
              </div>
              <div class="timeline-metric">
                <span>Outcome delta</span>
                <strong>{{ currentScenario.outcomeDelta }}</strong>
              </div>
              <div class="timeline-metric">
                <span>Confidence</span>
                <strong>{{ currentScenario.confidenceLabel }}</strong>
              </div>
            </div>

            <div class="scenario-list">
              <button
                v-for="scenario in overview.scenarios"
                :key="scenario.scenarioId"
                type="button"
                class="scenario-card"
                :class="{ 'is-selected': selectedScenarioId === scenario.scenarioId }"
                @click="selectedScenarioId = scenario.scenarioId"
              >
                <strong>{{ scenario.name }}</strong>
                <span>{{ scenario.currentTimeLabel }}</span>
                <p>{{ scenario.outcomeDelta }} - {{ scenario.mode }}</p>
              </button>
            </div>
          </div>
        </section>

        <section class="surface detail-surface">
          <div class="surface-heading">
            <div>
              <span class="panel-label">Details panel</span>
              <h2>Live narrative</h2>
            </div>
            <span class="mini-badge">Simulation feed</span>
          </div>

          <div class="detail-columns">
            <div>
              <h3>Operational signals</h3>
              <div class="alert-list">
                <article v-for="alert in overview.alerts" :key="alert.title" class="alert-row">
                  <div class="alert-heading">
                    <strong>{{ alert.title }}</strong>
                    <span :class="['severity-chip', `severity-${alert.severity.toLowerCase()}`]">
                      {{ alert.severity }}
                    </span>
                  </div>
                  <p>{{ alert.description }}</p>
                </article>
              </div>
            </div>

            <div>
              <h3>Event feed</h3>
              <div class="feed-list">
                <article v-for="event in overview.eventFeed" :key="event.id" class="feed-row">
                  <span>{{ event.timeLabel }}</span>
                  <div>
                    <strong>{{ event.title }}</strong>
                    <p>{{ event.description }}</p>
                  </div>
                </article>
              </div>
            </div>

            <div>
              <h3>Provider watch</h3>
              <div class="provider-list">
                <article v-for="provider in overview.providers" :key="provider.providerId" class="provider-row">
                  <div class="provider-heading">
                    <div>
                      <strong>{{ provider.providerName }}</strong>
                      <p>{{ provider.domain }} - {{ provider.syncStatusLabel }}</p>
                    </div>
                    <span :class="['severity-chip', `severity-${provider.healthStatus.toLowerCase()}`]">
                      {{ provider.healthStatus }}
                    </span>
                  </div>
                  <p>{{ provider.summary }}</p>
                  <span class="provider-meta">{{ provider.lastActivityLabel }}</span>
                </article>
              </div>
            </div>
          </div>
        </section>
      </section>

      <section class="surface catalog-surface">
        <div class="surface-heading">
          <div>
            <span class="panel-label">Connector catalog</span>
            <h2>Provider inventory</h2>
          </div>
          <div class="catalog-heading-meta">
            <span class="status-pill" :class="providerEditorTone">
              {{ catalogConnectionLabel }}
            </span>
            <span class="mini-badge">{{ providerCatalog.generatedAtLabel }}</span>
          </div>
        </div>

        <p v-if="providerCatalogErrorMessage" class="catalog-editor-note">
          {{ providerCatalogErrorMessage }}
        </p>

        <div class="catalog-grid">
          <article v-for="provider in providerCatalog.providers" :key="provider.providerId" class="catalog-card">
            <div class="catalog-card-head">
              <div>
                <strong>{{ provider.providerName }}</strong>
                <p>{{ provider.domain }} - {{ provider.kind }}</p>
              </div>
              <span :class="['severity-chip', `severity-${provider.healthStatus.toLowerCase()}`]">
                {{ provider.healthStatus }}
              </span>
            </div>

            <p class="catalog-summary">{{ provider.summary }}</p>

            <div class="catalog-meta">
              <span>{{ provider.configurationEnabled ? 'Enabled' : 'Disabled' }}</span>
              <span>{{ provider.configurationEnvironment }}</span>
              <span>{{ provider.configurationReadiness }}</span>
              <span>{{ provider.syncStatusLabel }}</span>
              <span>{{ provider.lastActivityLabel }}</span>
            </div>

            <div class="chip-row">
              <span v-for="capability in provider.capabilities" :key="capability" class="catalog-chip">{{ capability }}</span>
            </div>

            <div class="catalog-block">
              <span class="panel-label">Configuration</span>
              <div class="chip-row">
                <span v-for="field in provider.configuredFields" :key="`${provider.providerId}-configured-${field}`" class="catalog-chip">
                  {{ field }}
                </span>
                <span
                  v-for="field in provider.missingFields"
                  :key="`${provider.providerId}-missing-${field}`"
                  class="catalog-chip is-missing"
                >
                  Missing {{ field }}
                </span>
              </div>
            </div>

            <div class="catalog-block">
              <div class="catalog-editor-heading">
                <span class="panel-label">Runtime editor</span>
                <span class="catalog-editor-note">Changes persist to local appsettings.Local.json only.</span>
              </div>

              <label class="catalog-toggle">
                <input v-model="providerDraft(provider.providerId).enabled" type="checkbox" :disabled="!isProviderCatalogConnected || savingProviderId === provider.providerId">
                <span>Provider enabled</span>
              </label>

              <label class="catalog-field">
                <span>Environment</span>
                <input
                  v-model="providerDraft(provider.providerId).environment"
                  type="text"
                  :disabled="!isProviderCatalogConnected || savingProviderId === provider.providerId"
                >
              </label>

              <div class="catalog-editor-grid">
                <label
                  v-for="setting in provider.editableSettings"
                  :key="`${provider.providerId}-setting-${setting.key}`"
                  class="catalog-field"
                >
                  <span>{{ setting.key }}<small v-if="setting.required">Required</small></span>
                  <input
                    v-model="providerDraft(provider.providerId).settings[setting.key]"
                    type="text"
                    :disabled="!isProviderCatalogConnected || savingProviderId === provider.providerId"
                  >
                </label>
              </div>

              <div class="catalog-editor-actions">
                <button
                  type="button"
                  class="catalog-save-button"
                  :disabled="!isProviderCatalogConnected || savingProviderId === provider.providerId"
                  @click="saveProviderConfiguration(provider)"
                >
                  {{ savingProviderId === provider.providerId ? 'Saving...' : 'Save local configuration' }}
                </button>
                <span v-if="providerConfigurationNotice[provider.providerId]" class="catalog-editor-note">
                  {{ providerConfigurationNotice[provider.providerId] }}
                </span>
              </div>
            </div>

            <div class="catalog-block">
              <span class="panel-label">Supported read models</span>
              <div class="chip-row">
                <span v-for="readModel in provider.supportedReadModels" :key="readModel" class="catalog-chip is-muted">
                  {{ readModel }}
                </span>
              </div>
            </div>

            <div class="catalog-block">
              <span class="panel-label">Schema</span>
              <p>{{ provider.schemaResourceName }} - {{ provider.schemaFields.length }} fields</p>
              <ul class="schema-list">
                <li v-for="field in provider.schemaFields.slice(0, 4)" :key="`${provider.providerId}-${field.name}`">
                  <strong>{{ field.name }}</strong>
                  <span>{{ field.canonicalMapping }}</span>
                </li>
              </ul>
            </div>
          </article>
        </div>
      </section>
    </section>
  </main>
</template>

<style scoped>
.app-shell {
  display: grid;
  grid-template-columns: 248px minmax(0, 1fr);
  min-height: 100vh;
}

.sidebar {
  display: flex;
  flex-direction: column;
  gap: 24px;
  padding: 24px 20px;
  background: rgba(7, 15, 28, 0.92);
  border-right: 1px solid rgba(148, 163, 184, 0.12);
}

.brand {
  display: flex;
  align-items: center;
  gap: 14px;
}

.brand-mark {
  display: grid;
  place-items: center;
  width: 42px;
  height: 42px;
  color: #f8fafc;
  font-weight: 800;
  background: linear-gradient(135deg, #0f766e 0%, #2563eb 100%);
  border-radius: 8px;
}

.brand strong {
  display: block;
  font-size: 18px;
}

.brand p,
.eyebrow,
.panel-label,
.metric-panel p,
.route-row p,
.zone-tile p,
.alert-row p,
.feed-row p,
.sidebar-panel p {
  margin: 0;
  color: #94a3b8;
}

.nav-list {
  display: grid;
  gap: 8px;
}

.nav-item {
  display: grid;
  grid-template-columns: 38px minmax(0, 1fr);
  align-items: center;
  gap: 12px;
  padding: 10px 12px;
  color: #cbd5e1;
  text-decoration: none;
  border: 1px solid transparent;
  border-radius: 8px;
  background: transparent;
}

.nav-item.is-active {
  color: #f8fafc;
  border-color: rgba(96, 165, 250, 0.28);
  background: rgba(30, 41, 59, 0.92);
}

.nav-icon {
  display: grid;
  place-items: center;
  width: 38px;
  height: 38px;
  font-size: 12px;
  font-weight: 700;
  color: #e2e8f0;
  background: rgba(30, 41, 59, 0.84);
  border-radius: 8px;
}

.sidebar-panel {
  display: grid;
  gap: 8px;
  padding: 16px;
  background: rgba(15, 23, 42, 0.72);
  border: 1px solid rgba(148, 163, 184, 0.14);
  border-radius: 8px;
}

.panel-label {
  font-size: 12px;
  text-transform: uppercase;
  letter-spacing: 0;
}

.sidebar-list {
  margin: 0;
  padding-left: 18px;
  color: #cbd5e1;
}

.sidebar-list li + li {
  margin-top: 8px;
}

.workspace {
  display: grid;
  gap: 20px;
  min-width: 0;
  padding: 20px;
}

.topbar,
.surface,
.metric-panel {
  border: 1px solid rgba(148, 163, 184, 0.14);
  border-radius: 8px;
  background: rgba(15, 23, 42, 0.78);
  backdrop-filter: blur(12px);
}

.topbar {
  display: flex;
  align-items: start;
  justify-content: space-between;
  gap: 16px;
  padding: 20px 22px;
}

.headline {
  display: grid;
  gap: 6px;
}

.headline h1,
.surface-heading h2,
.detail-columns h3 {
  margin: 0;
}

.headline h1 {
  font-size: clamp(28px, 3vw, 38px);
  color: #f8fafc;
}

.status-pill,
.mini-badge,
.route-status,
.severity-chip {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-height: 30px;
  padding: 0 10px;
  font-size: 12px;
  font-weight: 700;
  border-radius: 999px;
  white-space: nowrap;
}

.status-pill.is-running {
  color: #d1fae5;
  background: rgba(6, 95, 70, 0.38);
  border: 1px solid rgba(52, 211, 153, 0.32);
}

.status-pill.is-paused {
  color: #fef3c7;
  background: rgba(120, 53, 15, 0.4);
  border: 1px solid rgba(251, 191, 36, 0.28);
}

.status-pill.is-loading {
  color: #dbeafe;
  background: rgba(30, 64, 175, 0.34);
  border: 1px solid rgba(96, 165, 250, 0.24);
}

.status-pill.is-warning {
  color: #fef3c7;
  background: rgba(120, 53, 15, 0.42);
  border: 1px solid rgba(251, 191, 36, 0.26);
}

.topbar-actions {
  display: flex;
  flex-wrap: wrap;
  align-items: end;
  justify-content: end;
  gap: 12px;
}

.refresh-button {
  min-height: 40px;
  padding: 0 14px;
  color: #f8fafc;
  background: rgba(37, 99, 235, 0.24);
  border: 1px solid rgba(96, 165, 250, 0.46);
  border-radius: 8px;
  cursor: pointer;
}

.refresh-button:disabled {
  cursor: progress;
  opacity: 0.65;
}

.scenario-select,
.compact-select {
  display: grid;
  gap: 6px;
}

.scenario-select span,
.compact-select span {
  font-size: 12px;
  color: #94a3b8;
}

.scenario-select select,
.compact-select select {
  min-width: 190px;
  padding: 10px 12px;
  color: #e2e8f0;
  background: rgba(15, 23, 42, 0.92);
  border: 1px solid rgba(148, 163, 184, 0.2);
  border-radius: 8px;
}

.control-group {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.control-group button,
.scenario-card {
  padding: 10px 14px;
  color: #e2e8f0;
  background: rgba(30, 41, 59, 0.96);
  border: 1px solid rgba(148, 163, 184, 0.18);
  border-radius: 8px;
  cursor: pointer;
}

.ghost-button.is-selected,
.scenario-card.is-selected {
  color: #f8fafc;
  background: rgba(37, 99, 235, 0.24);
  border-color: rgba(96, 165, 250, 0.46);
}

.summary-strip {
  display: grid;
  grid-template-columns: repeat(5, minmax(0, 1fr));
  gap: 16px;
}

.metric-panel {
  display: grid;
  gap: 10px;
  min-height: 124px;
  padding: 18px;
}

.metric-panel strong,
.timeline-metric strong {
  font-size: 24px;
  color: #f8fafc;
}

.workspace-grid {
  display: grid;
  grid-template-columns: minmax(0, 1.4fr) minmax(360px, 1fr);
  gap: 16px;
}

.surface {
  display: grid;
  gap: 16px;
  min-width: 0;
  padding: 18px;
}

.connection-surface {
  align-content: start;
}

.connection-grid {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 14px;
}

.connection-card,
.metric-panel,
.route-row,
.alert-row,
.feed-row,
.provider-row,
.scenario-card,
.timeline-metric,
.catalog-card {
  min-width: 0;
}

.connection-card {
  display: grid;
  gap: 10px;
  padding: 16px;
  border: 1px solid rgba(148, 163, 184, 0.14);
  border-radius: 8px;
  background: rgba(8, 17, 31, 0.55);
}

.connection-card-head {
  display: flex;
  align-items: start;
  justify-content: space-between;
  gap: 12px;
}

.connection-card strong,
.connection-summary {
  color: #f8fafc;
}

.connection-card p,
.connection-summary {
  margin: 0;
}

.connection-card p {
  color: #cbd5e1;
}

.connection-summary {
  color: #94a3b8;
  font-size: 13px;
}

.catalog-surface {
  margin-top: 16px;
}

.catalog-heading-meta {
  display: flex;
  flex-wrap: wrap;
  justify-content: end;
  gap: 10px;
}

.surface-heading {
  display: flex;
  align-items: start;
  justify-content: space-between;
  gap: 16px;
}

.surface-heading h2 {
  font-size: 22px;
  color: #f8fafc;
}

.mini-badge {
  color: #bfdbfe;
  background: rgba(30, 64, 175, 0.32);
  border: 1px solid rgba(96, 165, 250, 0.28);
}

.warehouse-surface {
  grid-column: 1;
}

.transport-surface {
  grid-column: 2;
  grid-row: 1 / span 2;
  align-content: start;
}

.controls-surface,
.detail-surface {
  grid-column: 1;
}

.surface-body,
.control-columns,
.detail-columns {
  display: grid;
  gap: 16px;
}

.warehouse-actions,
.warehouse-detail-grid,
.dock-strip {
  display: grid;
  gap: 12px;
}

.zone-strip {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 12px;
}

.zone-tile {
  display: grid;
  gap: 10px;
  min-height: 132px;
  padding: 14px;
  border: 1px solid rgba(148, 163, 184, 0.14);
  border-radius: 8px;
  background: rgba(8, 17, 31, 0.55);
}

.zone-head,
.alert-heading,
.route-row,
.feed-row,
.provider-heading,
.connection-banner {
  display: flex;
  align-items: start;
  justify-content: space-between;
  gap: 12px;
}

.zone-head strong,
.route-row strong,
.alert-heading strong,
.feed-row strong,
.provider-heading strong,
.scenario-card strong {
  color: #f8fafc;
}

.zone-label,
.warehouse-summary-note {
  margin: 0;
  color: #94a3b8;
}

.zone-meta {
  display: flex;
  flex-wrap: wrap;
  gap: 8px 10px;
  color: #cbd5e1;
  font-size: 13px;
}

.dock-card {
  display: grid;
  gap: 10px;
  padding: 14px;
  border: 1px solid rgba(148, 163, 184, 0.14);
  border-radius: 8px;
  background: rgba(8, 17, 31, 0.55);
}

.dock-card p {
  margin: 0;
  color: #cbd5e1;
}

.dock-status {
  display: inline-flex;
  align-items: center;
  min-height: 28px;
  padding: 0 10px;
  font-size: 12px;
  font-weight: 700;
  border-radius: 999px;
}

.dock-status.is-occupied {
  color: #fef3c7;
  background: rgba(120, 53, 15, 0.42);
  border: 1px solid rgba(251, 191, 36, 0.26);
}

.dock-status.is-available {
  color: #d1fae5;
  background: rgba(6, 95, 70, 0.38);
  border: 1px solid rgba(52, 211, 153, 0.32);
}

.map-stage {
  position: relative;
  min-height: 320px;
  overflow: hidden;
  border: 1px solid rgba(148, 163, 184, 0.14);
  border-radius: 8px;
  background:
    linear-gradient(180deg, rgba(13, 30, 50, 0.92) 0%, rgba(13, 23, 36, 0.98) 100%);
}

.map-grid {
  position: absolute;
  inset: 0;
  background-image:
    linear-gradient(rgba(51, 65, 85, 0.34) 1px, transparent 1px),
    linear-gradient(90deg, rgba(51, 65, 85, 0.34) 1px, transparent 1px);
  background-size: 48px 48px;
  mask-image: linear-gradient(180deg, rgba(255, 255, 255, 0.9), rgba(255, 255, 255, 0.4));
}

.route-marker {
  position: absolute;
  display: grid;
  gap: 2px;
  min-width: 88px;
  padding: 10px 12px;
  transform: translate(-50%, -50%);
  border: 1px solid rgba(148, 163, 184, 0.24);
  border-radius: 8px;
  background: rgba(15, 23, 42, 0.88);
  box-shadow: 0 18px 28px rgba(2, 6, 23, 0.34);
}

.route-marker strong {
  font-size: 14px;
  color: #f8fafc;
}

.route-marker span {
  font-size: 12px;
  color: #cbd5e1;
}

.route-marker.is-normal {
  border-color: rgba(74, 222, 128, 0.34);
}

.route-marker.is-risk {
  border-color: rgba(250, 204, 21, 0.34);
}

.route-marker.is-delayed {
  border-color: rgba(248, 113, 113, 0.36);
}

.map-legend {
  position: absolute;
  left: 16px;
  bottom: 16px;
  display: flex;
  flex-wrap: wrap;
  gap: 14px;
  padding: 10px 12px;
  color: #cbd5e1;
  background: rgba(15, 23, 42, 0.72);
  border: 1px solid rgba(148, 163, 184, 0.18);
  border-radius: 8px;
}

.legend-swatch {
  display: inline-block;
  width: 10px;
  height: 10px;
  margin-right: 6px;
  border-radius: 999px;
}

.legend-swatch.normal {
  background: #4ade80;
}

.legend-swatch.risk {
  background: #facc15;
}

.legend-swatch.delayed {
  background: #f87171;
}

.route-table,
.alert-list,
.feed-list,
.scenario-list,
.provider-list {
  display: grid;
  gap: 10px;
}

.route-row,
.alert-row,
.feed-row,
.provider-row,
.scenario-card,
.timeline-metric {
  padding: 14px;
  border: 1px solid rgba(148, 163, 184, 0.14);
  border-radius: 8px;
  background: rgba(8, 17, 31, 0.55);
}

.provider-meta {
  color: #94a3b8;
  font-size: 13px;
}

.catalog-grid {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 14px;
}

.catalog-card {
  display: grid;
  gap: 12px;
  padding: 16px;
  border: 1px solid rgba(148, 163, 184, 0.14);
  border-radius: 8px;
  background: rgba(8, 17, 31, 0.72);
}

.catalog-card-head {
  display: flex;
  align-items: start;
  justify-content: space-between;
  gap: 12px;
}

.catalog-card-head strong {
  color: #f8fafc;
}

.catalog-card-head p,
.catalog-summary,
.catalog-block p,
.schema-list span {
  margin: 0;
  color: #cbd5e1;
}

.catalog-meta {
  display: flex;
  flex-wrap: wrap;
  gap: 8px 12px;
  color: #94a3b8;
  font-size: 13px;
}

.catalog-meta span,
.catalog-summary,
.provider-meta,
.route-row p,
.zone-tile p,
.feed-row p,
.alert-row p,
.connection-card p,
.catalog-editor-note {
  overflow-wrap: anywhere;
}

.catalog-editor-heading,
.catalog-editor-actions {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
}

.catalog-editor-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 10px;
}

.catalog-field {
  display: grid;
  gap: 6px;
}

.catalog-field span,
.catalog-toggle span,
.catalog-editor-note {
  color: #94a3b8;
  font-size: 12px;
}

.catalog-field small {
  margin-left: 6px;
  color: #cbd5e1;
}

.catalog-field input {
  width: 100%;
  min-height: 40px;
  padding: 10px 12px;
  color: #e2e8f0;
  background: rgba(15, 23, 42, 0.92);
  border: 1px solid rgba(148, 163, 184, 0.2);
  border-radius: 8px;
}

.catalog-toggle {
  display: inline-flex;
  align-items: center;
  gap: 10px;
  color: #e2e8f0;
}

.catalog-toggle input {
  width: 16px;
  height: 16px;
}

.catalog-save-button {
  min-height: 40px;
  padding: 0 14px;
  color: #f8fafc;
  background: rgba(37, 99, 235, 0.24);
  border: 1px solid rgba(96, 165, 250, 0.46);
  border-radius: 8px;
  cursor: pointer;
}

.catalog-save-button:disabled,
.catalog-field input:disabled,
.catalog-toggle input:disabled {
  cursor: not-allowed;
  opacity: 0.6;
}

.chip-row {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.catalog-chip {
  display: inline-flex;
  align-items: center;
  min-height: 28px;
  padding: 0 10px;
  color: #e2e8f0;
  font-size: 12px;
  border: 1px solid rgba(96, 165, 250, 0.26);
  border-radius: 999px;
  background: rgba(37, 99, 235, 0.18);
}

.catalog-chip.is-muted {
  color: #cbd5e1;
  border-color: rgba(148, 163, 184, 0.18);
  background: rgba(30, 41, 59, 0.7);
}

.catalog-chip.is-missing {
  color: #fef3c7;
  border-color: rgba(251, 191, 36, 0.26);
  background: rgba(120, 53, 15, 0.42);
}

.catalog-block {
  display: grid;
  gap: 8px;
}

.schema-list {
  display: grid;
  gap: 8px;
  margin: 0;
  padding-left: 18px;
  color: #cbd5e1;
}

.schema-list li {
  display: grid;
  gap: 2px;
}

.route-meta {
  display: grid;
  justify-items: end;
  gap: 6px;
  min-width: 0;
  color: #cbd5e1;
}

.route-status {
  color: #dbeafe;
  background: rgba(30, 64, 175, 0.25);
  border: 1px solid rgba(96, 165, 250, 0.24);
}

.control-columns {
  grid-template-columns: repeat(2, minmax(0, 1fr));
}

.detail-columns {
  grid-template-columns: repeat(3, minmax(0, 1fr));
}

.timeline-panel {
  display: grid;
  gap: 10px;
}

.timeline-metric {
  display: grid;
  gap: 8px;
}

.timeline-metric span,
.feed-row span {
  color: #94a3b8;
}

.scenario-card {
  display: grid;
  gap: 6px;
  text-align: left;
}

.scenario-card span,
.scenario-card p {
  color: #cbd5e1;
  margin: 0;
}

.severity-critical {
  color: #fee2e2;
  background: rgba(153, 27, 27, 0.45);
  border: 1px solid rgba(248, 113, 113, 0.26);
}

.severity-warning {
  color: #fef3c7;
  background: rgba(120, 53, 15, 0.42);
  border: 1px solid rgba(251, 191, 36, 0.26);
}

.severity-info,
.severity-healthy {
  color: #dbeafe;
  background: rgba(30, 64, 175, 0.34);
  border: 1px solid rgba(96, 165, 250, 0.24);
}

.severity-degraded {
  color: #fef3c7;
  background: rgba(120, 53, 15, 0.42);
  border: 1px solid rgba(251, 191, 36, 0.26);
}

.severity-unhealthy {
  color: #fee2e2;
  background: rgba(153, 27, 27, 0.45);
  border: 1px solid rgba(248, 113, 113, 0.26);
}

.connection-banner p {
  margin: 0;
  color: #f8fafc;
}

@media (max-width: 1180px) {
  .workspace-grid,
  .control-columns,
  .detail-columns,
  .zone-strip {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }

  .summary-strip {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }

  .catalog-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }

  .connection-grid {
    grid-template-columns: 1fr;
  }

  .catalog-editor-grid {
    grid-template-columns: 1fr;
  }

  .transport-surface {
    grid-column: 1 / -1;
    grid-row: auto;
  }

  .warehouse-surface,
  .controls-surface,
  .detail-surface {
    grid-column: 1 / -1;
  }
}

@media (max-width: 860px) {
  .app-shell {
    grid-template-columns: 1fr;
  }

  .sidebar {
    padding-bottom: 12px;
  }

  .topbar,
  .surface-heading,
  .connection-card-head,
  .catalog-editor-heading,
  .catalog-editor-actions,
  .route-row,
  .feed-row,
  .alert-heading,
  .provider-heading,
  .connection-banner {
    flex-direction: column;
  }

  .topbar-actions {
    width: 100%;
    justify-content: start;
  }

  .summary-strip,
  .zone-strip,
  .control-columns,
  .detail-columns,
  .catalog-grid {
    grid-template-columns: 1fr;
  }

  .scenario-select select,
  .compact-select select {
    min-width: 0;
    width: 100%;
  }

  .route-meta {
    justify-items: start;
  }
}
</style>
