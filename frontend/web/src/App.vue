<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from "vue";
import WarehouseTwinScene from "./components/WarehouseTwinScene.vue";
import {
  buildFallbackOverview,
  buildFallbackRouteDetail,
  buildFallbackRouteOptimization,
  buildFallbackTransportSyncDiff,
  buildFallbackTransportSyncStatus,
  buildFallbackWarehouseDetail,
  buildFallbackProviderCatalog,
  simulationSpeeds,
  type ControlTowerOverviewView,
  type RouteDetailView,
  type RouteOptimizationView,
  type TransportSyncDiffView,
  type TransportSyncStatusView,
  type WarehouseDetailView,
  type ProviderCatalogView,
  type ProviderCatalogItemView,
} from "./data/controlTower";
import {
  loadAiRecommendation,
  type AiRecommendationEnvelopeView,
} from "./services/aiApi";
import { loadControlTowerOverview } from "./services/controlTowerApi";
import {
  buildFallbackOperationalActivity,
  loadOperationalActivity,
  type OperationalActivityView,
} from "./services/observabilityApi";
import { loadRouteOptimization } from "./services/optimizationApi";
import {
  loadProviderCatalog,
  updateProviderConfiguration,
  updateProviderSecret,
} from "./services/providerCatalogApi";
import { loadWarehouseProjection } from "./services/warehouseApi";
import { loadTransportProjection } from "./services/transportApi";
import {
  loadTransportSyncStatus,
  runTransportSync,
} from "./services/transportSyncApi";
import { loadTransportSyncDiff } from "./services/transportSyncDiffApi";

type DataConnectionState = "loading" | "api" | "fallback" | "stale";
type TransportSupportActionId =
  | "sync-import"
  | "sync-refresh"
  | "route-refresh"
  | "route-focus-import"
  | "optimization-refresh";

const navigationItems = [
  { label: "Control Tower", short: "CT", active: true },
  { label: "Warehouse", short: "WH", active: false },
  { label: "Transport", short: "TR", active: false },
  { label: "Scenarios", short: "SC", active: false },
];

const fallbackOverview = buildFallbackOverview();
const overview = ref<ControlTowerOverviewView>(fallbackOverview);
const warehouseDetail = ref<WarehouseDetailView>(
  buildFallbackWarehouseDetail(fallbackOverview.warehouses[0].warehouseId)
);
const routeDetail = ref<RouteDetailView>(
  buildFallbackRouteDetail(fallbackOverview.routes[0].routeId)
);
const isApiConnected = ref(false);
const isRefreshingWorkspace = ref(false);
const loadErrorMessage = ref<string | null>(null);
const overviewConnectionState = ref<DataConnectionState>("loading");
const warehouseDetailErrorMessage = ref<string | null>(null);
const warehouseConnectionState = ref<DataConnectionState>("loading");
const routeDetailErrorMessage = ref<string | null>(null);
const routeConnectionState = ref<DataConnectionState>("loading");
const transportSyncStatus = ref<TransportSyncStatusView>(
  buildFallbackTransportSyncStatus()
);
const transportSyncErrorMessage = ref<string | null>(null);
const transportSyncConnectionState = ref<DataConnectionState>("loading");
const isSyncingTransport = ref(false);
const transportSyncDiff = ref<TransportSyncDiffView>(
  buildFallbackTransportSyncDiff()
);
const transportSyncDiffConnectionState = ref<DataConnectionState>("loading");
const transportSyncDiffErrorMessage = ref<string | null>(null);
const providerCatalog = ref<ProviderCatalogView>(
  buildFallbackProviderCatalog()
);
const isProviderCatalogConnected = ref(false);
const providerCatalogErrorMessage = ref<string | null>(null);
const providerCatalogConnectionState = ref<DataConnectionState>("loading");
const aiRecommendation = ref<AiRecommendationEnvelopeView | null>(null);
const aiQuestion = ref("Which route should the dispatcher review first?");
const aiRecommendationErrorMessage = ref<string | null>(null);
const aiConnectionState = ref<DataConnectionState>("loading");
const isRequestingAi = ref(false);
const operationalActivity = ref<OperationalActivityView>(
  buildFallbackOperationalActivity()
);
const operationalActivityErrorMessage = ref<string | null>(null);
const operationalConnectionState = ref<DataConnectionState>("loading");
const isRefreshingOperationalActivity = ref(false);
const routeOptimization = ref<RouteOptimizationView>(
  buildFallbackRouteOptimization(routeDetail.value)
);
const routeOptimizationErrorMessage = ref<string | null>(null);
const routeOptimizationConnectionState = ref<DataConnectionState>("loading");
const isOptimizingRoute = ref(false);
const savingProviderId = ref<string | null>(null);
const providerConfigurationNotice = ref<Record<string, string>>({});
const providerConfigurationDrafts = ref<
  Record<
    string,
    { enabled: boolean; environment: string; settings: Record<string, string> }
  >
>({});
const secretKeyDrafts = ref<Record<string, string>>({});
const savingSecretProviderId = ref<string | null>(null);
const secretSaveNotice = ref<Record<string, string>>({});
const selectedScenarioId = ref(fallbackOverview.scenarios[0].scenarioId);
const selectedWarehouseId = ref(fallbackOverview.warehouses[0].warehouseId);
const selectedRouteId = ref(fallbackOverview.routes[0].routeId);
const simulationState = ref<"Running" | "Paused">("Running");
const selectedSpeed = ref(simulationSpeeds[1]);
let connectionRecoveryHandle = 0;

const currentScenario = computed(
  () =>
    overview.value.scenarios.find(
      (scenario) => scenario.scenarioId === selectedScenarioId.value
    ) ?? overview.value.scenarios[0]
);

const currentWarehouse = computed(
  () =>
    overview.value.warehouses.find(
      (warehouse) => warehouse.warehouseId === selectedWarehouseId.value
    ) ?? overview.value.warehouses[0]
);

const currentRoute = computed(
  () =>
    overview.value.routes.find(
      (route) => route.routeId === selectedRouteId.value
    ) ?? overview.value.routes[0]
);

const currentRiskCount = computed(
  () =>
    overview.value.alerts.filter((alert) => alert.severity !== "Info").length
);
const delayedRouteCount = computed(
  () =>
    overview.value.routes.filter((route) => route.status !== "On time").length
);
const degradedProviderCount = computed(
  () =>
    overview.value.providers.filter(
      (provider) => provider.healthStatus !== "Healthy"
    ).length
);
const warehouseUtilization = computed(() => {
  const warehouse = currentWarehouse.value;
  return Math.round((warehouse.storedPalletCount / warehouse.slotCount) * 100);
});

function toneForConnectionState(
  state: DataConnectionState
): "is-loading" | "is-running" | "is-paused" | "is-warning" {
  if (state === "loading") {
    return "is-loading";
  }

  if (state === "api") {
    return "is-running";
  }

  if (state === "stale") {
    return "is-warning";
  }

  return "is-paused";
}

function labelForConnectionState(
  state: DataConnectionState,
  apiLabel: string,
  fallbackLabel: string
): string {
  if (state === "loading") {
    return "Loading";
  }

  if (state === "api") {
    return apiLabel;
  }

  if (state === "stale") {
    return `Stale ${apiLabel}`;
  }

  return fallbackLabel;
}

const scenarioStatusTone = computed(() =>
  simulationState.value === "Running" ? "is-running" : "is-paused"
);
const providerEditorTone = computed(() =>
  toneForConnectionState(providerCatalogConnectionState.value)
);
const dataConnectionTone = computed(() =>
  toneForConnectionState(overviewConnectionState.value)
);
const warehouseProjectionTone = computed(() =>
  toneForConnectionState(warehouseConnectionState.value)
);
const transportProjectionTone = computed(() =>
  toneForConnectionState(routeConnectionState.value)
);
const transportSyncTone = computed(() =>
  toneForConnectionState(transportSyncConnectionState.value)
);
const overviewConnectionLabel = computed(() =>
  labelForConnectionState(
    overviewConnectionState.value,
    "API live",
    "Fallback active"
  )
);
const warehouseProjectionLabel = computed(() =>
  labelForConnectionState(
    warehouseConnectionState.value,
    "Warehouse API live",
    "Warehouse fallback"
  )
);
const transportProjectionLabel = computed(() =>
  labelForConnectionState(
    routeConnectionState.value,
    "Transport API live",
    "Transport fallback"
  )
);
const transportSyncLabel = computed(() =>
  labelForConnectionState(
    transportSyncConnectionState.value,
    "Transport sync live",
    "Transport sync fallback"
  )
);
const catalogConnectionLabel = computed(() =>
  labelForConnectionState(
    providerCatalogConnectionState.value,
    "Editable local API",
    "Read-only fallback"
  )
);
const aiWorkflowTone = computed(() =>
  toneForConnectionState(aiConnectionState.value)
);
const aiWorkflowLabel = computed(() =>
  labelForConnectionState(
    aiConnectionState.value,
    "AI workflow live",
    "AI fallback"
  )
);
const operationalWorkflowTone = computed(() =>
  toneForConnectionState(operationalConnectionState.value)
);
const operationalWorkflowLabel = computed(() =>
  labelForConnectionState(
    operationalConnectionState.value,
    "Operational trace live",
    "Operational trace fallback"
  )
);
const optimizationWorkflowTone = computed(() =>
  toneForConnectionState(routeOptimizationConnectionState.value)
);
const optimizationWorkflowLabel = computed(() =>
  labelForConnectionState(
    routeOptimizationConnectionState.value,
    "Optimization live",
    "Optimization fallback"
  )
);
const aiRecommendationView = computed(
  () => aiRecommendation.value?.recommendation ?? null
);
const currentRemainingRouteOrder = computed(() => {
  const pendingStopSequences = new Set(
    routeDetail.value.deliveries
      .filter((delivery) => delivery.status !== "Completed")
      .map((delivery) => delivery.stopSequence)
  );

  const pendingStops = routeDetail.value.stops
    .filter((stop) => pendingStopSequences.has(stop.sequence))
    .map((stop) => stop.name);

  return pendingStops.length > 0
    ? pendingStops
    : routeDetail.value.stops.map((stop) => stop.name);
});
const transportSyncSourceLabel = computed(() => {
  switch (transportSyncStatus.value.source) {
    case "live":
      return "Live snapshot";
    case "configuration-incomplete":
      return "Configuration incomplete";
    case "disabled":
      return "Sync disabled";
    default:
      return "Demo fallback snapshot";
  }
});
const transportSyncHealthClass = computed(() => {
  switch (transportSyncStatus.value.healthStatus) {
    case "Unhealthy":
      return "severity-unhealthy";
    case "Degraded":
      return "severity-degraded";
    default:
      return "severity-healthy";
  }
});
const transportSyncDeltaSummary = computed(() => {
  const imported = new Set(transportSyncStatus.value.importedRouteReferences);
  const current = new Set(
    overview.value.routes.map((route) => route.reference)
  );

  const newRoutes = transportSyncStatus.value.importedRouteReferences.filter(
    (reference) => !current.has(reference)
  );
  const missingRoutes = overview.value.routes
    .map((route) => route.reference)
    .filter((reference) => !imported.has(reference));

  if (!transportSyncStatus.value.hasPersistedSnapshot) {
    return "No persisted transport snapshot has been imported yet.";
  }

  if (newRoutes.length === 0 && missingRoutes.length === 0) {
    return "Current route board matches the latest imported snapshot.";
  }

  const parts: string[] = [];
  if (newRoutes.length > 0) {
    parts.push(
      `${newRoutes.length} new route${newRoutes.length > 1 ? "s" : ""}`
    );
  }

  if (missingRoutes.length > 0) {
    parts.push(
      `${missingRoutes.length} route${
        missingRoutes.length > 1 ? "s" : ""
      } no longer present`
    );
  }

  return `${parts.join(" and ")} versus the current route board.`;
});
const transportSyncHistory = computed(() =>
  operationalActivity.value.workflowRuns.filter(
    (run) => run.workflowKind === "transport-sync-import"
  )
);
const transportChangedDiffs = computed(() =>
  transportSyncDiff.value.routeDiffs.filter(
    (item) => item.changeType !== "Unchanged"
  )
);
const latestImportedRoute = computed(() => {
  const importedRefs = new Set(
    transportSyncStatus.value.importedRouteReferences
  );

  return (
    overview.value.routes.find((route) => importedRefs.has(route.reference)) ??
    null
  );
});
const transportSupportActions = computed(() => [
  {
    id: "sync-import" as const,
    label: isSyncingTransport.value ? "Syncing..." : "Import snapshot",
    detail: "Pull the latest transport snapshot into the persisted local view.",
    disabled: isSyncingTransport.value,
  },
  {
    id: "sync-refresh" as const,
    label: "Refresh sync",
    detail: "Reload sync evidence without running a new import.",
    disabled: isSyncingTransport.value,
  },
  {
    id: "route-refresh" as const,
    label: "Refresh route",
    detail: `Reload the selected route projection for ${routeDetail.value.reference}.`,
    disabled: routeConnectionState.value === "loading",
  },
  {
    id: "optimization-refresh" as const,
    label: isOptimizingRoute.value ? "Reviewing..." : "Re-run optimization",
    detail:
      "Rebuild the optimization review for the current route and scenario.",
    disabled:
      isOptimizingRoute.value || routeConnectionState.value === "loading",
  },
  {
    id: "route-focus-import" as const,
    label:
      latestImportedRoute.value &&
      latestImportedRoute.value.routeId !== selectedRouteId.value
        ? `Focus ${latestImportedRoute.value.reference}`
        : "Imported route in focus",
    detail: latestImportedRoute.value
      ? "Jump to a route that is confirmed in the latest imported transport snapshot."
      : "No imported route is available yet in the current transport board.",
    disabled:
      latestImportedRoute.value === null ||
      latestImportedRoute.value.routeId === selectedRouteId.value,
  },
]);
const transportRecoveryCues = computed(() => {
  const cues: Array<{
    title: string;
    detail: string;
    actionId: TransportSupportActionId;
    actionLabel: string;
  }> = [];

  if (!transportSyncStatus.value.hasPersistedSnapshot) {
    cues.push({
      title: "No persisted transport snapshot yet",
      detail:
        "Run a manual import before the demo so the route board can rely on a saved transport baseline.",
      actionId: "sync-import",
      actionLabel: "Import snapshot",
    });
  }

  if (
    transportSyncConnectionState.value === "fallback" ||
    transportSyncConnectionState.value === "stale" ||
    transportSyncStatus.value.source === "configuration-incomplete" ||
    transportSyncStatus.value.healthStatus !== "Healthy"
  ) {
    cues.push({
      title: "Sync posture needs attention",
      detail:
        transportSyncErrorMessage.value ??
        transportSyncStatus.value.syncDetail ??
        "Refresh sync evidence or re-import a snapshot before trusting the current board.",
      actionId: transportSyncStatus.value.hasPersistedSnapshot
        ? "sync-refresh"
        : "sync-import",
      actionLabel: transportSyncStatus.value.hasPersistedSnapshot
        ? "Refresh sync"
        : "Import snapshot",
    });
  }

  if (
    routeConnectionState.value === "fallback" ||
    routeConnectionState.value === "stale"
  ) {
    cues.push({
      title: "Route detail is not fully live",
      detail:
        routeDetailErrorMessage.value ??
        `Reload ${routeDetail.value.reference} before using it for support or optimization decisions.`,
      actionId: "route-refresh",
      actionLabel: "Refresh route",
    });
  }

  if (
    routeOptimizationConnectionState.value === "fallback" ||
    routeOptimizationConnectionState.value === "stale"
  ) {
    cues.push({
      title: "Optimization review should be refreshed",
      detail:
        routeOptimizationErrorMessage.value ??
        "The current optimization snapshot may not match the latest route state.",
      actionId: "optimization-refresh",
      actionLabel: "Re-run optimization",
    });
  }

  if (
    transportSyncStatus.value.hasPersistedSnapshot &&
    !transportSyncStatus.value.importedRouteReferences.includes(
      routeDetail.value.reference
    ) &&
    latestImportedRoute.value
  ) {
    cues.push({
      title: "Selected route is outside the latest import",
      detail: `${routeDetail.value.reference} is still visible in the board, but ${latestImportedRoute.value.reference} is the nearest confirmed route from the latest imported snapshot.`,
      actionId: "route-focus-import",
      actionLabel: `Focus ${latestImportedRoute.value.reference}`,
    });
  }

  return cues;
});
const selectedRouteStoryHighlights = computed(() => {
  const highlights: Array<{ title: string; detail: string }> = [];
  const routeReference = routeDetail.value.reference;
  const importedRefs = new Set(
    transportSyncStatus.value.importedRouteReferences
  );
  const pendingDeliveries = routeDetail.value.deliveries.filter(
    (delivery) => delivery.status !== "Completed"
  ).length;

  highlights.push({
    title: importedRefs.has(routeReference)
      ? "Present in latest import"
      : "Not present in latest import",
    detail: importedRefs.has(routeReference)
      ? `${routeReference} is part of the latest imported transport snapshot.`
      : `${routeReference} is visible in the current board but missing from the latest imported snapshot.`,
  });

  highlights.push({
    title: "Delivery progress",
    detail: `${routeDetail.completedDeliveryCount} completed and ${pendingDeliveries} still pending across ${routeDetail.stopCount} stops.`,
  });

  highlights.push({
    title: "Sync freshness",
    detail: transportSyncStatus.value.lastImportedAtLabel
      ? `Latest transport snapshot imported ${transportSyncStatus.value.lastImportedAtLabel}.`
      : "No transport snapshot has been imported yet for this tenant.",
  });

  const latestSyncRun = transportSyncHistory.value[0];
  if (latestSyncRun) {
    highlights.push({
      title: "Latest sync note",
      detail: latestSyncRun.summary,
    });
  }

  return highlights;
});
const tenantSummary = computed(() => {
  if (overviewConnectionState.value === "api") {
    return "API-backed control tower";
  }

  if (overviewConnectionState.value === "stale") {
    return "Last successful API snapshot retained";
  }

  if (overviewConnectionState.value === "loading") {
    return "Connecting to local services";
  }

  return "Fallback demo data active";
});
const connectionMessage = computed(() => {
  return (
    loadErrorMessage.value ??
    warehouseDetailErrorMessage.value ??
    routeDetailErrorMessage.value ??
    transportSyncErrorMessage.value ??
    providerCatalogErrorMessage.value ??
    aiRecommendationErrorMessage.value ??
    operationalActivityErrorMessage.value ??
    routeOptimizationErrorMessage.value ??
    "All local data surfaces are ready."
  );
});

const aiQuickPrompts = [
  "Which warehouse needs attention right now?",
  "Which route should the dispatcher review first?",
  "What data is still missing before we can be confident?",
];

function toggleSimulation(): void {
  simulationState.value =
    simulationState.value === "Running" ? "Paused" : "Running";
}

function setSimulationSpeed(speed: (typeof simulationSpeeds)[number]): void {
  selectedSpeed.value = speed;
}

function runTransportSupportAction(actionId: TransportSupportActionId): void {
  switch (actionId) {
    case "sync-import":
      void triggerTransportSync();
      return;
    case "sync-refresh":
      void refreshTransportSyncStatus(false);
      return;
    case "route-refresh":
      void refreshTransportProjection(selectedRouteId.value, false);
      return;
    case "route-focus-import":
      if (latestImportedRoute.value) {
        selectedRouteId.value = latestImportedRoute.value.routeId;
      }
      return;
    case "optimization-refresh":
      void refreshRouteOptimization(false);
      return;
  }
}

function syncProviderDrafts(catalog: ProviderCatalogView): void {
  providerConfigurationDrafts.value = Object.fromEntries(
    catalog.providers.map((provider) => [
      provider.providerId,
      {
        enabled: provider.configurationEnabled,
        environment: provider.configurationEnvironment,
        settings: Object.fromEntries(
          provider.editableSettings.map((setting) => [
            setting.key,
            setting.value,
          ])
        ),
      },
    ])
  );
}

function providerDraft(providerId: string): {
  enabled: boolean;
  environment: string;
  settings: Record<string, string>;
} {
  const existing = providerConfigurationDrafts.value[providerId];

  if (existing) {
    return existing;
  }

  providerConfigurationDrafts.value[providerId] = {
    enabled: true,
    environment: "local-demo",
    settings: {},
  };

  return providerConfigurationDrafts.value[providerId];
}

async function runAiPrompt(question: string): Promise<void> {
  aiQuestion.value = question;
  await refreshAiRecommendation(false);
}

function applyOverviewResult(
  result: Awaited<ReturnType<typeof loadControlTowerOverview>>,
  preserveExisting: boolean
): void {
  const keepCurrentSnapshot =
    preserveExisting &&
    result.source === "fallback" &&
    (overviewConnectionState.value === "api" ||
      overviewConnectionState.value === "stale");

  if (!keepCurrentSnapshot) {
    overview.value = result.overview;
    selectedScenarioId.value = result.overview.scenarios.some(
      (scenario) => scenario.scenarioId === selectedScenarioId.value
    )
      ? selectedScenarioId.value
      : result.overview.scenarios[0]?.scenarioId ??
        fallbackOverview.scenarios[0].scenarioId;
    selectedWarehouseId.value = result.overview.warehouses.some(
      (warehouse) => warehouse.warehouseId === selectedWarehouseId.value
    )
      ? selectedWarehouseId.value
      : result.overview.warehouses[0]?.warehouseId ??
        fallbackOverview.warehouses[0].warehouseId;
    selectedRouteId.value = result.overview.routes.some(
      (route) => route.routeId === selectedRouteId.value
    )
      ? selectedRouteId.value
      : result.overview.routes[0]?.routeId ??
        fallbackOverview.routes[0].routeId;
  }

  overviewConnectionState.value = keepCurrentSnapshot
    ? "stale"
    : result.source === "api"
    ? "api"
    : "fallback";
  isApiConnected.value = overviewConnectionState.value !== "fallback";
  loadErrorMessage.value =
    keepCurrentSnapshot && result.errorMessage
      ? `${result.errorMessage} Showing the last successful API snapshot.`
      : result.errorMessage;
}

function applyCatalogResult(
  result: Awaited<ReturnType<typeof loadProviderCatalog>>,
  preserveExisting: boolean
): void {
  const keepCurrentSnapshot =
    preserveExisting &&
    result.source === "fallback" &&
    (providerCatalogConnectionState.value === "api" ||
      providerCatalogConnectionState.value === "stale");

  if (!keepCurrentSnapshot) {
    providerCatalog.value = result.catalog;
    syncProviderDrafts(result.catalog);
  }

  providerCatalogConnectionState.value = keepCurrentSnapshot
    ? "stale"
    : result.source === "api"
    ? "api"
    : "fallback";
  isProviderCatalogConnected.value =
    providerCatalogConnectionState.value !== "fallback";
  providerCatalogErrorMessage.value =
    keepCurrentSnapshot && result.errorMessage
      ? `${result.errorMessage} Keeping the last successful editable catalog.`
      : result.errorMessage;
}

function applyWarehouseProjectionResult(
  result: Awaited<ReturnType<typeof loadWarehouseProjection>>,
  preserveExisting: boolean,
  requestedWarehouseId: string
): void {
  const keepCurrentSnapshot =
    preserveExisting &&
    result.source === "fallback" &&
    warehouseDetail.value.warehouseId === requestedWarehouseId &&
    (warehouseConnectionState.value === "api" ||
      warehouseConnectionState.value === "stale");

  if (!keepCurrentSnapshot) {
    warehouseDetail.value = result.warehouse;
  }

  warehouseConnectionState.value = keepCurrentSnapshot
    ? "stale"
    : result.source === "api"
    ? "api"
    : "fallback";
  warehouseDetailErrorMessage.value =
    keepCurrentSnapshot && result.errorMessage
      ? `${result.errorMessage} Keeping the last successful warehouse projection.`
      : result.errorMessage;
}

function applyTransportProjectionResult(
  result: Awaited<ReturnType<typeof loadTransportProjection>>,
  preserveExisting: boolean,
  requestedRouteId: string
): void {
  const keepCurrentSnapshot =
    preserveExisting &&
    result.source === "fallback" &&
    routeDetail.value.routeId === requestedRouteId &&
    (routeConnectionState.value === "api" ||
      routeConnectionState.value === "stale");

  if (!keepCurrentSnapshot) {
    routeDetail.value = result.route;
  }

  routeConnectionState.value = keepCurrentSnapshot
    ? "stale"
    : result.source === "api"
    ? "api"
    : "fallback";
  routeDetailErrorMessage.value =
    keepCurrentSnapshot && result.errorMessage
      ? `${result.errorMessage} Keeping the last successful transport projection.`
      : result.errorMessage;
}

function applyTransportSyncResult(
  result: Awaited<ReturnType<typeof loadTransportSyncStatus>>,
  preserveExisting: boolean
): void {
  const keepCurrentSnapshot =
    preserveExisting &&
    result.source === "fallback" &&
    transportSyncStatus.value.hasPersistedSnapshot &&
    (transportSyncConnectionState.value === "api" ||
      transportSyncConnectionState.value === "stale");

  if (!keepCurrentSnapshot) {
    transportSyncStatus.value = result.status;
  }

  transportSyncConnectionState.value = keepCurrentSnapshot
    ? "stale"
    : result.source === "api"
    ? "api"
    : "fallback";
  transportSyncErrorMessage.value =
    keepCurrentSnapshot && result.errorMessage
      ? `${result.errorMessage} Keeping the last successful transport sync evidence.`
      : result.errorMessage;
}

function applyTransportSyncDiffResult(
  result: Awaited<ReturnType<typeof loadTransportSyncDiff>>,
  preserveExisting: boolean
): void {
  const keepCurrentSnapshot =
    preserveExisting &&
    result.source === "fallback" &&
    (transportSyncDiffConnectionState.value === "api" ||
      transportSyncDiffConnectionState.value === "stale");

  if (!keepCurrentSnapshot) {
    transportSyncDiff.value = result.diff;
  }

  transportSyncDiffConnectionState.value = keepCurrentSnapshot
    ? "stale"
    : result.source === "api"
    ? "api"
    : "fallback";
  transportSyncDiffErrorMessage.value =
    keepCurrentSnapshot && result.errorMessage
      ? `${result.errorMessage} Keeping the last successful transport diff evidence.`
      : result.errorMessage;
}

function applyAiRecommendationResult(
  result: Awaited<ReturnType<typeof loadAiRecommendation>>,
  preserveExisting: boolean
): void {
  const keepCurrentSnapshot =
    preserveExisting &&
    result.source === "fallback" &&
    (aiConnectionState.value === "api" || aiConnectionState.value === "stale");

  if (!keepCurrentSnapshot) {
    aiRecommendation.value = result.workflow;
  }

  aiConnectionState.value = keepCurrentSnapshot
    ? "stale"
    : result.source === "api"
    ? "api"
    : "fallback";
  aiRecommendationErrorMessage.value =
    keepCurrentSnapshot && result.errorMessage
      ? `${result.errorMessage} Keeping the last successful AI recommendation.`
      : result.errorMessage;
}

function applyOperationalActivityResult(
  result: Awaited<ReturnType<typeof loadOperationalActivity>>,
  preserveExisting: boolean
): void {
  const keepCurrentSnapshot =
    preserveExisting &&
    result.source === "fallback" &&
    (operationalConnectionState.value === "api" ||
      operationalConnectionState.value === "stale");

  if (!keepCurrentSnapshot) {
    operationalActivity.value = result.activity;
  }

  operationalConnectionState.value = keepCurrentSnapshot
    ? "stale"
    : result.source === "api"
    ? "api"
    : "fallback";
  operationalActivityErrorMessage.value =
    keepCurrentSnapshot && result.errorMessage
      ? `${result.errorMessage} Keeping the last successful operational trace.`
      : result.errorMessage;
}

function applyRouteOptimizationResult(
  result: Awaited<ReturnType<typeof loadRouteOptimization>>,
  preserveExisting: boolean,
  requestedRouteId: string
): void {
  const keepCurrentSnapshot =
    preserveExisting &&
    result.source === "fallback" &&
    routeOptimization.value.routeId === requestedRouteId &&
    (routeOptimizationConnectionState.value === "api" ||
      routeOptimizationConnectionState.value === "stale");

  if (!keepCurrentSnapshot) {
    routeOptimization.value = result.optimization;
  }

  routeOptimizationConnectionState.value = keepCurrentSnapshot
    ? "stale"
    : result.source === "api"
    ? "api"
    : "fallback";
  routeOptimizationErrorMessage.value =
    keepCurrentSnapshot && result.errorMessage
      ? `${result.errorMessage} Keeping the last successful optimization review.`
      : result.errorMessage;
}

async function refreshWarehouseProjection(
  warehouseId: string,
  preserveExisting = true
): Promise<void> {
  if (!preserveExisting) {
    warehouseConnectionState.value = "loading";
  }

  const result = await loadWarehouseProjection(warehouseId);
  applyWarehouseProjectionResult(result, preserveExisting, warehouseId);
}

async function refreshTransportProjection(
  routeId: string,
  preserveExisting = true
): Promise<void> {
  if (!preserveExisting) {
    routeConnectionState.value = "loading";
  }

  const result = await loadTransportProjection(routeId);
  applyTransportProjectionResult(result, preserveExisting, routeId);
}

async function refreshTransportSyncStatus(
  preserveExisting = true
): Promise<void> {
  if (!preserveExisting) {
    transportSyncConnectionState.value = "loading";
  }

  const result = await loadTransportSyncStatus();
  applyTransportSyncResult(result, preserveExisting);
}

async function refreshTransportSyncDiff(
  preserveExisting = true
): Promise<void> {
  if (!preserveExisting) {
    transportSyncDiffConnectionState.value = "loading";
  }

  const result = await loadTransportSyncDiff();
  applyTransportSyncDiffResult(result, preserveExisting);
}

async function refreshAiRecommendation(
  preserveExisting = true,
  syncOperationalTrace = true
): Promise<void> {
  isRequestingAi.value = true;

  if (!preserveExisting) {
    aiConnectionState.value = "loading";
  }

  try {
    const result = await loadAiRecommendation({
      question: aiQuestion.value,
      scenarioId: selectedScenarioId.value,
    });
    applyAiRecommendationResult(result, preserveExisting);

    if (syncOperationalTrace) {
      await refreshOperationalActivity(true);
    }
  } finally {
    isRequestingAi.value = false;
  }
}

async function refreshOperationalActivity(
  preserveExisting = true
): Promise<void> {
  isRefreshingOperationalActivity.value = true;

  if (!preserveExisting) {
    operationalConnectionState.value = "loading";
  }

  try {
    const result = await loadOperationalActivity();
    applyOperationalActivityResult(result, preserveExisting);
  } finally {
    isRefreshingOperationalActivity.value = false;
  }
}

async function refreshRouteOptimization(
  preserveExisting = true,
  syncOperationalTrace = true
): Promise<void> {
  isOptimizingRoute.value = true;

  if (!preserveExisting) {
    routeOptimizationConnectionState.value = "loading";
  }

  try {
    const result = await loadRouteOptimization(
      routeDetail.value,
      selectedScenarioId.value
    );
    applyRouteOptimizationResult(
      result,
      preserveExisting,
      routeDetail.value.routeId
    );

    if (syncOperationalTrace) {
      await refreshOperationalActivity(true);
    }
  } finally {
    isOptimizingRoute.value = false;
  }
}

async function refreshWorkspace(preserveExisting = true): Promise<void> {
  isRefreshingWorkspace.value = true;

  if (!preserveExisting) {
    overviewConnectionState.value = "loading";
    warehouseConnectionState.value = "loading";
    routeConnectionState.value = "loading";
    transportSyncConnectionState.value = "loading";
    transportSyncDiffConnectionState.value = "loading";
    providerCatalogConnectionState.value = "loading";
    aiConnectionState.value = "loading";
    operationalConnectionState.value = "loading";
    routeOptimizationConnectionState.value = "loading";
  }

  const [overviewResult, catalogResult] = await Promise.all([
    loadControlTowerOverview(),
    loadProviderCatalog(),
  ]);
  applyOverviewResult(overviewResult, preserveExisting);
  applyCatalogResult(catalogResult, preserveExisting);
  await refreshWarehouseProjection(selectedWarehouseId.value, preserveExisting);
  await refreshTransportProjection(selectedRouteId.value, preserveExisting);
  await refreshTransportSyncStatus(preserveExisting);
  await refreshTransportSyncDiff(preserveExisting);
  await refreshRouteOptimization(preserveExisting, false);
  await refreshAiRecommendation(preserveExisting, false);
  await refreshOperationalActivity(preserveExisting);
  isRefreshingWorkspace.value = false;

  const hasPartialFallback =
    [
      overviewConnectionState.value,
      warehouseConnectionState.value,
      routeConnectionState.value,
      transportSyncConnectionState.value,
      transportSyncDiffConnectionState.value,
      providerCatalogConnectionState.value,
      aiConnectionState.value,
      operationalConnectionState.value,
      routeOptimizationConnectionState.value,
    ].some((state) => state === "fallback") &&
    [
      overviewConnectionState.value,
      warehouseConnectionState.value,
      routeConnectionState.value,
      transportSyncConnectionState.value,
      transportSyncDiffConnectionState.value,
      providerCatalogConnectionState.value,
      aiConnectionState.value,
      operationalConnectionState.value,
      routeOptimizationConnectionState.value,
    ].some((state) => state === "api" || state === "stale");

  if (hasPartialFallback) {
    window.clearTimeout(connectionRecoveryHandle);
    connectionRecoveryHandle = window.setTimeout(() => {
      void refreshWorkspace(true);
    }, 1500);
  }
}

async function triggerTransportSync(): Promise<void> {
  isSyncingTransport.value = true;

  try {
    if (transportSyncConnectionState.value !== "stale") {
      transportSyncConnectionState.value = "loading";
    }

    const result = await runTransportSync();
    applyTransportSyncResult(result, true);
    const diffResult = await loadTransportSyncDiff();
    applyTransportSyncDiffResult(diffResult, true);

    if (result.source === "api") {
      await refreshWorkspace(true);
    }
  } finally {
    isSyncingTransport.value = false;
  }
}

async function refreshProviderCatalog(preserveExisting = true): Promise<void> {
  if (!preserveExisting) {
    providerCatalogConnectionState.value = "loading";
  }

  const catalogResult = await loadProviderCatalog();
  applyCatalogResult(catalogResult, preserveExisting);
}

async function saveProviderConfiguration(
  provider: ProviderCatalogItemView
): Promise<void> {
  const draft = providerDraft(provider.providerId);
  providerConfigurationNotice.value = {
    ...providerConfigurationNotice.value,
    [provider.providerId]: "",
  };

  savingProviderId.value = provider.providerId;

  try {
    await updateProviderConfiguration({
      providerId: provider.providerId,
      enabled: draft.enabled,
      environment: draft.environment,
      settings: draft.settings,
    });

    await refreshProviderCatalog(true);
    await refreshOperationalActivity(true);
    providerConfigurationNotice.value = {
      ...providerConfigurationNotice.value,
      [provider.providerId]: "Saved locally.",
    };
  } catch (error) {
    providerConfigurationNotice.value = {
      ...providerConfigurationNotice.value,
      [provider.providerId]:
        error instanceof Error
          ? error.message
          : "Unable to save provider configuration.",
    };
  } finally {
    savingProviderId.value = null;
  }
}

async function setProviderSecret(
  provider: ProviderCatalogItemView,
  secretKey: string
): Promise<void> {
  const secretValue = secretKeyDrafts.value[provider.providerId] ?? "";

  secretSaveNotice.value = {
    ...secretSaveNotice.value,
    [provider.providerId]: "",
  };

  savingSecretProviderId.value = provider.providerId;

  try {
    await updateProviderSecret({
      providerId: provider.providerId,
      secretKey,
      secretValue,
    });

    // Clear the draft after saving so the value is not held in memory.
    secretKeyDrafts.value = {
      ...secretKeyDrafts.value,
      [provider.providerId]: "",
    };

    await refreshProviderCatalog(true);
    secretSaveNotice.value = {
      ...secretSaveNotice.value,
      [provider.providerId]: "API key stored in local secrets file.",
    };
  } catch (error) {
    secretSaveNotice.value = {
      ...secretSaveNotice.value,
      [provider.providerId]:
        error instanceof Error ? error.message : "Unable to save API key.",
    };
  } finally {
    savingSecretProviderId.value = null;
  }
}

onMounted(async () => {
  await refreshWorkspace(false);
});

watch(selectedWarehouseId, (nextWarehouseId, previousWarehouseId) => {
  if (nextWarehouseId === previousWarehouseId || isRefreshingWorkspace.value) {
    return;
  }

  void refreshWarehouseProjection(nextWarehouseId, false);
});

watch(selectedRouteId, (nextRouteId, previousRouteId) => {
  if (nextRouteId === previousRouteId || isRefreshingWorkspace.value) {
    return;
  }

  void (async () => {
    await refreshTransportProjection(nextRouteId, false);
    await refreshRouteOptimization(false);
  })();
});

watch(selectedScenarioId, (nextScenarioId, previousScenarioId) => {
  if (nextScenarioId === previousScenarioId || isRefreshingWorkspace.value) {
    return;
  }

  void (async () => {
    await refreshRouteOptimization(false);
    await refreshAiRecommendation(false);
  })();
});

onBeforeUnmount(() => {
  window.clearTimeout(connectionRecoveryHandle);
});
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
          <button
            type="button"
            class="refresh-button"
            :disabled="isRefreshingWorkspace"
            @click="refreshWorkspace(true)"
          >
            {{ isRefreshingWorkspace ? "Refreshing..." : "Refresh data" }}
          </button>

          <label class="scenario-select">
            <span>Scenario</span>
            <select v-model="selectedScenarioId">
              <option
                v-for="scenario in overview.scenarios"
                :key="scenario.scenarioId"
                :value="scenario.scenarioId"
              >
                {{ scenario.name }}
              </option>
            </select>
          </label>

          <div class="control-group" aria-label="Simulation controls">
            <button type="button" @click="toggleSimulation">
              {{ simulationState === "Running" ? "Pause" : "Resume" }}
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
          <p>
            Seed {{ currentScenario.seed }} -
            {{ currentScenario.injectedEventCount }} injected events
          </p>
        </article>

        <article class="metric-panel">
          <span class="panel-label">Warehouse occupancy</span>
          <strong>{{ warehouseUtilization }}%</strong>
          <p>
            {{ currentWarehouse.storedPalletCount }} pallets across
            {{ currentWarehouse.slotCount }} slots
          </p>
        </article>

        <article class="metric-panel">
          <span class="panel-label">Transport watch</span>
          <strong>{{ delayedRouteCount }} routes off plan</strong>
          <p>{{ overview.routes.length }} active routes on the ETA board</p>
        </article>

        <article class="metric-panel">
          <span class="panel-label">Operational risk</span>
          <strong>{{ currentRiskCount }} active signals</strong>
          <p>{{ overview.alerts[0]?.title ?? "No active alert" }}</p>
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
          <span class="mini-badge">{{
            isRefreshingWorkspace ? "Refreshing" : "Stable view"
          }}</span>
        </div>

        <div class="connection-grid">
          <article class="connection-card">
            <div class="connection-card-head">
              <div>
                <strong>Overview feed</strong>
                <p>
                  Scenario, warehouse, transport, alerts, and provider watch.
                </p>
              </div>
              <span class="status-pill" :class="dataConnectionTone">{{
                overviewConnectionLabel
              }}</span>
            </div>
            <p>
              {{
                loadErrorMessage ??
                "Overview endpoint is serving the operator workspace."
              }}
            </p>
          </article>

          <article class="connection-card">
            <div class="connection-card-head">
              <div>
                <strong>Warehouse projection</strong>
                <p>
                  Zone posture, dock availability, and twin-facing warehouse
                  detail.
                </p>
              </div>
              <span class="status-pill" :class="warehouseProjectionTone">{{
                warehouseProjectionLabel
              }}</span>
            </div>
            <p>
              {{
                warehouseDetailErrorMessage ??
                `Detailed warehouse projection updated ${warehouseDetail.updatedAtLabel}.`
              }}
            </p>
          </article>

          <article class="connection-card">
            <div class="connection-card-head">
              <div>
                <strong>Transport projection</strong>
                <p>Route detail, shipment posture, and delivery progress.</p>
              </div>
              <span class="status-pill" :class="transportProjectionTone">{{
                transportProjectionLabel
              }}</span>
            </div>
            <p>
              {{
                routeDetailErrorMessage ??
                `Detailed transport projection updated ${routeDetail.updatedAtLabel}.`
              }}
            </p>
          </article>

          <article class="connection-card">
            <div class="connection-card-head">
              <div>
                <strong>Transport synchronization</strong>
                <p>
                  Last imported transport snapshot, source posture, and sync
                  evidence.
                </p>
              </div>
              <span class="status-pill" :class="transportSyncTone">{{
                transportSyncLabel
              }}</span>
            </div>
            <p>
              {{
                transportSyncErrorMessage ??
                transportSyncStatus.syncDetail ??
                `Latest transport sync is tracking ${transportSyncStatus.importedRouteCount} imported routes.`
              }}
            </p>
          </article>

          <article class="connection-card">
            <div class="connection-card-head">
              <div>
                <strong>Provider catalog</strong>
                <p>
                  Connector inventory, runtime posture, and local editing flow.
                </p>
              </div>
              <span class="status-pill" :class="providerEditorTone">{{
                catalogConnectionLabel
              }}</span>
            </div>
            <p>
              {{
                providerCatalogErrorMessage ??
                "Provider catalog is available for local inspection and editing."
              }}
            </p>
          </article>

          <article class="connection-card">
            <div class="connection-card-head">
              <div>
                <strong>Route optimization</strong>
                <p>
                  Dispatcher resequencing workflow and solver-backed recovery
                  plans.
                </p>
              </div>
              <span class="status-pill" :class="optimizationWorkflowTone">{{
                optimizationWorkflowLabel
              }}</span>
            </div>
            <p>
              {{
                routeOptimizationErrorMessage ??
                `Optimization review ready for ${routeOptimization.routeReference}.`
              }}
            </p>
          </article>

          <article class="connection-card">
            <div class="connection-card-head">
              <div>
                <strong>Operational trace</strong>
                <p>
                  Persisted snapshots, workflow runs, and recent audit evidence.
                </p>
              </div>
              <span class="status-pill" :class="operationalWorkflowTone">{{
                operationalWorkflowLabel
              }}</span>
            </div>
            <p>
              {{
                operationalActivityErrorMessage ??
                `Trace panel ready with ${operationalActivity.workflowRuns.length} workflow runs.`
              }}
            </p>
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
              <span class="status-pill" :class="warehouseProjectionTone">{{
                warehouseProjectionLabel
              }}</span>
              <label class="compact-select">
                <span>View</span>
                <select v-model="selectedWarehouseId">
                  <option
                    v-for="warehouse in overview.warehouses"
                    :key="warehouse.warehouseId"
                    :value="warehouse.warehouseId"
                  >
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
                <article
                  v-for="zone in warehouseDetail.zones"
                  :key="zone.code"
                  class="zone-tile"
                >
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
                <article
                  v-for="dock in warehouseDetail.docks"
                  :key="dock.code"
                  class="dock-card"
                >
                  <div class="zone-head">
                    <strong>{{ dock.code }}</strong>
                    <span
                      class="dock-status"
                      :class="
                        dock.status === 'Occupied'
                          ? 'is-occupied'
                          : 'is-available'
                      "
                    >
                      {{ dock.status }}
                    </span>
                  </div>
                  <p>{{ dock.activityLabel }}</p>
                </article>
                <p class="warehouse-summary-note">
                  Detailed warehouse projection updated
                  {{ warehouseDetail.updatedAtLabel }}.
                </p>
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
            <span class="mini-badge">{{ transportProjectionLabel }}</span>
          </div>

          <div class="map-stage" aria-label="Transport map placeholder">
            <div class="map-grid"></div>
            <div
              v-for="route in overview.routes"
              :key="route.routeId"
              class="route-marker"
              :class="
                route.status === 'Delayed'
                  ? 'is-delayed'
                  : route.status === 'At risk'
                  ? 'is-risk'
                  : 'is-normal'
              "
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
            <button
              v-for="route in overview.routes"
              :key="route.routeId"
              type="button"
              class="route-row"
              :class="{ 'is-selected': selectedRouteId === route.routeId }"
              @click="selectedRouteId = route.routeId"
            >
              <div>
                <strong>{{ route.reference }}</strong>
                <p>
                  {{ route.truckReference }} - {{ route.stopCount }} stops -
                  {{ route.shipmentCount }} shipments
                </p>
              </div>
              <div class="route-meta">
                <span class="route-status">{{ route.status }}</span>
                <span>{{ route.nextEtaLabel }}</span>
              </div>
            </button>
          </div>

          <article class="transport-sync-card">
            <div class="catalog-editor-heading">
              <div>
                <span class="panel-label">Transport sync</span>
                <h3>Imported snapshot posture</h3>
              </div>
              <div class="catalog-heading-meta">
                <span class="status-pill" :class="transportSyncTone">
                  {{ transportSyncLabel }}
                </span>
                <button
                  type="button"
                  class="catalog-save-button"
                  :disabled="isSyncingTransport"
                  @click="triggerTransportSync"
                >
                  {{ isSyncingTransport ? "Syncing..." : "Import snapshot" }}
                </button>
              </div>
            </div>

            <p class="catalog-summary">
              {{
                transportSyncErrorMessage ??
                transportSyncStatus.syncDetail ??
                transportSyncStatus.healthSummary
              }}
            </p>

            <div class="transport-sync-grid">
              <div class="transport-sync-metric">
                <span class="panel-label">Source</span>
                <strong>{{ transportSyncSourceLabel }}</strong>
                <span class="status-pill" :class="transportSyncHealthClass">
                  {{ transportSyncStatus.healthStatus }}
                </span>
              </div>

              <div class="transport-sync-metric">
                <span class="panel-label">Imported routes</span>
                <strong>{{ transportSyncStatus.importedRouteCount }}</strong>
                <span>{{
                  transportSyncStatus.lastImportedAtLabel
                    ? `Imported ${transportSyncStatus.lastImportedAtLabel}`
                    : "No imported snapshot yet"
                }}</span>
              </div>

              <div class="transport-sync-metric">
                <span class="panel-label">Sync posture</span>
                <strong>{{ transportSyncStatus.syncStatusLabel }}</strong>
                <span>{{ transportSyncStatus.lastActivityLabel }}</span>
              </div>
            </div>

            <div class="transport-sync-delta">
              <span class="panel-label">Route delta</span>
              <p>{{ transportSyncDeltaSummary }}</p>
            </div>

            <div class="catalog-block">
              <span class="panel-label">Imported route references</span>
              <div class="chip-row">
                <span
                  v-for="reference in transportSyncStatus.importedRouteReferences"
                  :key="`sync-route-${reference}`"
                  class="catalog-chip"
                >
                  {{ reference }}
                </span>
                <span
                  v-if="
                    transportSyncStatus.importedRouteReferences.length === 0
                  "
                  class="catalog-chip is-muted"
                >
                  No imported routes yet
                </span>
              </div>
            </div>
          </article>

          <div class="transport-support-grid">
            <article class="transport-story-card">
              <div class="catalog-editor-heading">
                <div>
                  <span class="panel-label">Operator actions</span>
                  <h3>Transport support shortcuts</h3>
                </div>
                <span class="mini-badge">{{
                  transportSupportActions.length
                }}</span>
              </div>

              <div class="transport-support-actions">
                <button
                  v-for="action in transportSupportActions"
                  :key="action.id"
                  type="button"
                  class="transport-support-action"
                  :disabled="action.disabled"
                  @click="runTransportSupportAction(action.id)"
                >
                  <strong>{{ action.label }}</strong>
                  <span>{{ action.detail }}</span>
                </button>
              </div>
            </article>

            <article class="transport-story-card">
              <div class="catalog-editor-heading">
                <div>
                  <span class="panel-label">Recovery cues</span>
                  <h3>What to check next</h3>
                </div>
                <span class="mini-badge">{{
                  transportRecoveryCues.length
                }}</span>
              </div>

              <ul class="transport-story-list">
                <li
                  v-for="cue in transportRecoveryCues"
                  :key="`${cue.title}-${cue.actionId}`"
                  class="transport-recovery-item"
                >
                  <strong>{{ cue.title }}</strong>
                  <span>{{ cue.detail }}</span>
                  <button
                    type="button"
                    class="catalog-save-button"
                    @click="runTransportSupportAction(cue.actionId)"
                  >
                    {{ cue.actionLabel }}
                  </button>
                </li>
                <li v-if="transportRecoveryCues.length === 0">
                  <strong>Transport posture looks healthy</strong>
                  <span>
                    Sync, route detail, and optimization are aligned enough for
                    a clean operator demo.
                  </span>
                </li>
              </ul>
            </article>
          </div>

          <div class="transport-story-grid">
            <article class="transport-story-card">
              <div class="catalog-editor-heading">
                <div>
                  <span class="panel-label">Route storyline</span>
                  <h3>{{ routeDetail.reference }}</h3>
                </div>
                <span class="mini-badge">{{ routeDetail.status }}</span>
              </div>

              <ul class="transport-story-list">
                <li
                  v-for="highlight in selectedRouteStoryHighlights"
                  :key="`${routeDetail.routeId}-${highlight.title}`"
                >
                  <strong>{{ highlight.title }}</strong>
                  <span>{{ highlight.detail }}</span>
                </li>
              </ul>
            </article>

            <article class="transport-story-card">
              <div class="catalog-editor-heading">
                <div>
                  <span class="panel-label">Sync timeline</span>
                  <h3>Recent imports</h3>
                </div>
                <span class="mini-badge">{{
                  transportSyncHistory.length
                }}</span>
              </div>

              <ul class="transport-story-list">
                <li v-for="run in transportSyncHistory" :key="run.id">
                  <strong>{{ run.createdAtLabel }}</strong>
                  <span>{{ run.summary }}</span>
                  <span>{{ run.status }} via {{ run.source }}</span>
                </li>
                <li v-if="transportSyncHistory.length === 0">
                  <strong>No import history yet</strong>
                  <span>
                    Trigger an import to start building a transport sync
                    timeline for this tenant.
                  </span>
                </li>
              </ul>
            </article>

            <article class="transport-story-card">
              <div class="catalog-editor-heading">
                <div>
                  <span class="panel-label">Historical diff</span>
                  <h3>Latest vs previous import</h3>
                </div>
                <span class="mini-badge">{{
                  transportChangedDiffs.length
                }}</span>
              </div>

              <p class="catalog-summary">
                {{ transportSyncDiffErrorMessage ?? transportSyncDiff.detail }}
              </p>

              <div class="transport-sync-grid">
                <div class="transport-sync-metric">
                  <span class="panel-label">Latest import</span>
                  <strong
                    >{{ transportSyncDiff.latestRouteCount }} routes</strong
                  >
                  <span>{{
                    transportSyncDiff.latestImportedAtLabel ?? "Unavailable"
                  }}</span>
                </div>

                <div class="transport-sync-metric">
                  <span class="panel-label">Previous import</span>
                  <strong
                    >{{ transportSyncDiff.previousRouteCount }} routes</strong
                  >
                  <span>{{
                    transportSyncDiff.previousImportedAtLabel ?? "Unavailable"
                  }}</span>
                </div>

                <div class="transport-sync-metric">
                  <span class="panel-label">Delta</span>
                  <strong
                    >{{ transportSyncDiff.changedRouteCount }} changed</strong
                  >
                  <span>
                    {{ transportSyncDiff.addedRouteCount }} added,
                    {{ transportSyncDiff.removedRouteCount }} removed
                  </span>
                </div>
              </div>

              <ul class="transport-story-list">
                <li
                  v-for="item in transportChangedDiffs"
                  :key="`${item.routeReference}-${item.changeType}`"
                  class="transport-route-diff-item"
                >
                  <strong
                    >{{ item.routeReference }} - {{ item.changeType }}</strong
                  >
                  <span>{{ item.summary }}</span>
                </li>
                <li v-if="transportChangedDiffs.length === 0">
                  <strong>No route-level deltas yet</strong>
                  <span>
                    Import another transport snapshot to unlock before/after
                    drill-down evidence.
                  </span>
                </li>
              </ul>
            </article>
          </div>

          <div class="transport-detail-grid">
            <article class="route-detail-card">
              <div class="route-detail-head">
                <div>
                  <span class="panel-label">Selected route</span>
                  <h3>{{ currentRoute.reference }}</h3>
                  <p>
                    {{ routeDetail.driverName }} -
                    {{ routeDetail.truckReference }} -
                    {{ routeDetail.truckStatus }}
                  </p>
                </div>
                <span class="status-pill" :class="transportProjectionTone">{{
                  transportProjectionLabel
                }}</span>
              </div>

              <div class="route-detail-meta">
                <span>{{ routeDetail.status }}</span>
                <span>{{ routeDetail.updatedAtLabel }}</span>
                <span
                  >{{ routeDetail.totalLoadKilograms }} /
                  {{ routeDetail.truckCapacityKilograms }} kg</span
                >
                <span
                  >{{ routeDetail.completedDeliveryCount }} completed
                  deliveries</span
                >
              </div>

              <div class="transport-detail-columns">
                <section class="transport-detail-block">
                  <span class="panel-label">Stops</span>
                  <ul class="detail-list">
                    <li
                      v-for="stop in routeDetail.stops"
                      :key="`${routeDetail.routeId}-stop-${stop.sequence}`"
                    >
                      <strong>{{ stop.sequence }}. {{ stop.name }}</strong>
                      <span
                        >{{ stop.coordinateLabel }} -
                        {{ stop.timeWindowLabel }}</span
                      >
                    </li>
                  </ul>
                </section>

                <section class="transport-detail-block">
                  <span class="panel-label">Shipments</span>
                  <ul class="detail-list">
                    <li
                      v-for="shipment in routeDetail.shipments"
                      :key="`${routeDetail.routeId}-shipment-${shipment.reference}`"
                    >
                      <strong>{{ shipment.reference }}</strong>
                      <span
                        >{{ shipment.status }} -
                        {{ shipment.loadWeightKilograms }} kg -
                        {{ shipment.orderReference }}</span
                      >
                    </li>
                  </ul>
                </section>

                <section class="transport-detail-block">
                  <span class="panel-label">Deliveries</span>
                  <ul class="detail-list">
                    <li
                      v-for="delivery in routeDetail.deliveries"
                      :key="`${routeDetail.routeId}-delivery-${delivery.reference}`"
                    >
                      <strong>{{ delivery.reference }}</strong>
                      <span
                        >Stop {{ delivery.stopSequence }} -
                        {{ delivery.stopName }} -
                        {{ delivery.shipmentReference }}</span
                      >
                      <span>{{ delivery.status }}</span>
                    </li>
                  </ul>
                </section>
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
                :class="{
                  'is-selected': selectedScenarioId === scenario.scenarioId,
                }"
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
                <article
                  v-for="alert in overview.alerts"
                  :key="alert.title"
                  class="alert-row"
                >
                  <div class="alert-heading">
                    <strong>{{ alert.title }}</strong>
                    <span
                      :class="[
                        'severity-chip',
                        `severity-${alert.severity.toLowerCase()}`,
                      ]"
                    >
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
                <article
                  v-for="event in overview.eventFeed"
                  :key="event.id"
                  class="feed-row"
                >
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
                <article
                  v-for="provider in overview.providers"
                  :key="provider.providerId"
                  class="provider-row"
                >
                  <div class="provider-heading">
                    <div>
                      <strong>{{ provider.providerName }}</strong>
                      <p>
                        {{ provider.domain }} - {{ provider.syncStatusLabel }}
                      </p>
                    </div>
                    <span
                      :class="[
                        'severity-chip',
                        `severity-${provider.healthStatus.toLowerCase()}`,
                      ]"
                    >
                      {{ provider.healthStatus }}
                    </span>
                  </div>
                  <p>{{ provider.summary }}</p>
                  <span class="provider-meta">{{
                    provider.lastActivityLabel
                  }}</span>
                </article>
              </div>
            </div>
          </div>
        </section>
      </section>

      <section class="surface ai-surface">
        <div class="surface-heading">
          <div>
            <span class="panel-label">AI workflow</span>
            <h2>Bounded recommendations</h2>
          </div>
          <div class="catalog-heading-meta">
            <span class="status-pill" :class="aiWorkflowTone">
              {{ aiWorkflowLabel }}
            </span>
            <span class="mini-badge">
              {{
                aiConnectionState === "loading"
                  ? "Waiting for answer"
                  : aiRecommendation?.source === "api"
                  ? "Python service"
                  : "Local fallback"
              }}
            </span>
          </div>
        </div>

        <div class="ai-workflow-grid">
          <article class="ai-query-panel">
            <div class="catalog-editor-heading">
              <div>
                <span class="panel-label">Ask the workflow</span>
                <p class="catalog-summary">
                  The current overview snapshot is the grounding context for
                  every answer.
                </p>
              </div>
            </div>

            <label class="catalog-field ai-question-field">
              <span>Question</span>
              <textarea
                v-model="aiQuestion"
                rows="3"
                placeholder="Which route should the dispatcher review first?"
              ></textarea>
            </label>

            <div class="chip-row">
              <button
                v-for="prompt in aiQuickPrompts"
                :key="prompt"
                type="button"
                class="catalog-chip is-muted ai-prompt-chip"
                @click="runAiPrompt(prompt)"
              >
                {{ prompt }}
              </button>
            </div>

            <div class="catalog-editor-actions">
              <button
                type="button"
                class="catalog-save-button"
                :disabled="isRequestingAi"
                @click="refreshAiRecommendation(false)"
              >
                {{ isRequestingAi ? "Analyzing..." : "Ask AI" }}
              </button>
              <span class="catalog-editor-note">
                {{
                  aiRecommendationErrorMessage ??
                  "The workflow stays projection-first and never invents missing state."
                }}
              </span>
            </div>

            <div class="catalog-meta ai-context-meta">
              <span>Tenant {{ overview.tenantId }}</span>
              <span>Scenario {{ currentScenario.name }}</span>
              <span>Routes {{ overview.routes.length }}</span>
              <span>Warehouses {{ overview.warehouses.length }}</span>
            </div>
          </article>

          <article class="ai-answer-panel">
            <div class="connection-banner">
              <div>
                <span class="panel-label">Recommendation</span>
                <p>
                  {{
                    aiRecommendationView?.directAnswer ||
                    "Run a question to see a grounded answer."
                  }}
                </p>
              </div>
              <span
                :class="[
                  'severity-chip',
                  aiRecommendationView?.confidenceLevel === 'high'
                    ? 'severity-healthy'
                    : aiRecommendationView?.confidenceLevel === 'medium'
                    ? 'severity-warning'
                    : 'severity-unhealthy',
                ]"
              >
                {{
                  aiRecommendationView?.confidenceLevel
                    ? aiRecommendationView.confidenceLevel.toUpperCase()
                    : "LOW"
                }}
              </span>
            </div>

            <div v-if="aiRecommendationView" class="ai-answer-body">
              <div class="route-detail-meta">
                <span>Intent: {{ aiRecommendationView.intent }}</span>
                <span>Source: {{ aiRecommendation?.source }}</span>
                <span
                  >Agents:
                  {{ aiRecommendationView.specialistAgents.join(", ") }}</span
                >
              </div>

              <div class="transport-detail-columns">
                <section class="transport-detail-block">
                  <span class="panel-label">Evidence</span>
                  <ul class="detail-list">
                    <li
                      v-for="item in aiRecommendationView.evidence"
                      :key="`${item.source}-${item.detail}`"
                    >
                      <strong>{{ item.source }}</strong>
                      <span>{{ item.detail }}</span>
                    </li>
                  </ul>
                </section>

                <section class="transport-detail-block">
                  <span class="panel-label">Assumptions</span>
                  <ul class="detail-list">
                    <li v-if="aiRecommendationView.assumptions.length === 0">
                      <strong>None</strong>
                      <span
                        >The recommendation is grounded entirely in the live
                        projections.</span
                      >
                    </li>
                    <li
                      v-for="assumption in aiRecommendationView.assumptions"
                      :key="assumption"
                    >
                      <strong>Assumption</strong>
                      <span>{{ assumption }}</span>
                    </li>
                  </ul>
                </section>

                <section class="transport-detail-block">
                  <span class="panel-label">Recommended actions</span>
                  <ul class="detail-list">
                    <li
                      v-for="action in aiRecommendationView.recommendedActions"
                      :key="`${action.title}-${action.priority}`"
                    >
                      <strong>{{ action.title }}</strong>
                      <span>{{ action.rationale }}</span>
                      <span>Priority: {{ action.priority }}</span>
                    </li>
                  </ul>
                </section>
              </div>

              <div class="transport-detail-columns">
                <section class="transport-detail-block">
                  <span class="panel-label">Missing data</span>
                  <ul class="detail-list">
                    <li v-if="aiRecommendationView.missingData.length === 0">
                      <strong>None</strong>
                      <span
                        >No critical context is missing for this answer.</span
                      >
                    </li>
                    <li
                      v-for="item in aiRecommendationView.missingData"
                      :key="item"
                    >
                      <strong>Missing</strong>
                      <span>{{ item }}</span>
                    </li>
                  </ul>
                </section>

                <section class="transport-detail-block">
                  <span class="panel-label">Scenario note</span>
                  <p class="provider-meta">
                    {{
                      aiRecommendationView.alternativeScenarioNote ??
                      "No alternate scenario note was returned."
                    }}
                  </p>
                </section>

                <section class="transport-detail-block">
                  <span class="panel-label">Answer posture</span>
                  <p class="provider-meta">
                    The workflow returns
                    {{ aiRecommendationView.confidenceLevel }} confidence and
                    surfaces evidence before any recommendation.
                  </p>
                </section>
              </div>
            </div>
          </article>
        </div>
      </section>

      <section class="surface optimization-surface">
        <div class="surface-heading">
          <div>
            <span class="panel-label">Dispatcher optimization</span>
            <h2>Route recovery review</h2>
          </div>
          <div class="catalog-heading-meta">
            <span class="status-pill" :class="optimizationWorkflowTone">
              {{ optimizationWorkflowLabel }}
            </span>
            <span class="mini-badge">
              {{
                isOptimizingRoute
                  ? "Recomputing plan"
                  : routeOptimization.solverBackend
              }}
            </span>
          </div>
        </div>

        <div class="optimization-grid">
          <article class="optimization-summary-card">
            <div class="connection-card-head">
              <div>
                <strong>{{ routeOptimization.routeReference }}</strong>
                <p>
                  {{ routeDetail.truckReference }} -
                  {{ routeDetail.driverName }} - {{ currentScenario.name }}
                </p>
              </div>
              <span class="status-pill" :class="transportProjectionTone">
                {{ routeDetail.status }}
              </span>
            </div>

            <div class="catalog-meta ai-context-meta">
              <span
                >Current remaining stops
                {{ currentRemainingRouteOrder.length }}</span
              >
              <span
                >Completed deliveries
                {{ routeDetail.completedDeliveryCount }}</span
              >
              <span
                >Objective {{ routeOptimization.objectiveScore ?? "n/a" }}</span
              >
            </div>

            <div class="catalog-editor-actions">
              <button
                type="button"
                class="catalog-save-button"
                :disabled="isOptimizingRoute"
                @click="refreshRouteOptimization(false)"
              >
                {{
                  isOptimizingRoute ? "Optimizing..." : "Re-run optimization"
                }}
              </button>
              <span class="catalog-editor-note">
                {{
                  routeOptimizationErrorMessage ??
                  "The optimization workflow compares the remaining route posture against solver-backed alternatives."
                }}
              </span>
            </div>
          </article>

          <article class="optimization-plan-card">
            <div class="surface-heading">
              <div>
                <span class="panel-label">Current remaining plan</span>
                <h3>{{ currentRoute.reference }}</h3>
              </div>
              <span class="mini-badge"
                >{{ currentRemainingRouteOrder.length }} stops</span
              >
            </div>

            <ol class="optimization-order-list">
              <li
                v-for="stopName in currentRemainingRouteOrder"
                :key="`current-${stopName}`"
              >
                {{ stopName }}
              </li>
            </ol>
          </article>

          <article class="optimization-plan-card">
            <div class="surface-heading">
              <div>
                <span class="panel-label">Recommended plan</span>
                <h3>{{ routeOptimization.status }}</h3>
              </div>
              <span class="mini-badge">{{
                routeOptimization.solverBackend
              }}</span>
            </div>

            <ol class="optimization-order-list">
              <li
                v-for="stopName in routeOptimization.orderedStopReferences"
                :key="`optimized-${stopName}`"
              >
                {{ stopName }}
              </li>
            </ol>
          </article>
        </div>

        <div class="optimization-grid is-detail">
          <article class="optimization-detail-card">
            <span class="panel-label">Explanation</span>
            <p class="catalog-summary">
              {{ routeOptimization.explanation.selectedVehicleReason }}
            </p>
            <p class="catalog-summary">
              {{ routeOptimization.explanation.prioritizationReason }}
            </p>

            <div class="chip-row">
              <span
                v-for="constraint in routeOptimization.explanation
                  .tightConstraints"
                :key="constraint"
                class="catalog-chip is-missing"
              >
                {{ constraint }}
              </span>
              <span
                v-if="
                  routeOptimization.explanation.tightConstraints.length === 0
                "
                class="catalog-chip is-muted"
              >
                No tight constraints
              </span>
            </div>
          </article>

          <article class="optimization-detail-card">
            <span class="panel-label">Trade-offs</span>
            <ul class="detail-list">
              <li
                v-for="tradeOff in routeOptimization.explanation.tradeOffs"
                :key="tradeOff"
              >
                <strong>Trade-off</strong>
                <span>{{ tradeOff }}</span>
              </li>
              <li v-if="routeOptimization.explanation.infeasibilityReason">
                <strong>Infeasibility</strong>
                <span>{{
                  routeOptimization.explanation.infeasibilityReason
                }}</span>
              </li>
            </ul>
          </article>

          <article class="optimization-detail-card">
            <span class="panel-label">Alternatives</span>
            <ul class="detail-list">
              <li
                v-for="alternative in routeOptimization.alternatives"
                :key="alternative.label"
              >
                <strong>{{ alternative.label }}</strong>
                <span>{{
                  alternative.orderedStopReferences.join(" -> ")
                }}</span>
                <span>{{ alternative.summary }}</span>
              </li>
              <li v-if="routeOptimization.alternatives.length === 0">
                <strong>No alternatives</strong>
                <span
                  >The workflow kept a single recommended plan for this route
                  posture.</span
                >
              </li>
            </ul>
          </article>
        </div>
      </section>

      <section class="surface operations-surface">
        <div class="surface-heading">
          <div>
            <span class="panel-label">Operational trace</span>
            <h2>Recent persisted activity</h2>
          </div>
          <div class="catalog-heading-meta">
            <span class="status-pill" :class="operationalWorkflowTone">
              {{ operationalWorkflowLabel }}
            </span>
            <button
              type="button"
              class="catalog-save-button"
              :disabled="isRefreshingOperationalActivity"
              @click="refreshOperationalActivity(false)"
            >
              {{
                isRefreshingOperationalActivity
                  ? "Refreshing trace..."
                  : "Refresh trace"
              }}
            </button>
          </div>
        </div>

        <div class="operations-summary-grid">
          <article class="optimization-summary-card">
            <span class="panel-label">Trace posture</span>
            <p class="catalog-summary">
              {{
                operationalActivityErrorMessage ??
                "The operator workspace can now inspect persisted snapshots, workflow runs, and audit entries without opening backend files directly."
              }}
            </p>
            <div class="catalog-meta">
              <span
                >{{ operationalActivity.workflowRuns.length }} workflow
                runs</span
              >
              <span
                >{{
                  operationalActivity.projectionSnapshots.length
                }}
                snapshots</span
              >
              <span
                >{{ operationalActivity.auditEntries.length }} audit
                entries</span
              >
            </div>
          </article>

          <article class="optimization-summary-card">
            <span class="panel-label">Why it matters</span>
            <p class="catalog-summary">
              This is the demo bridge between product behavior and operational
              evidence: every recommendation, optimization, and projection
              refresh can now be traced back to persisted backend state.
            </p>
          </article>
        </div>

        <div class="operations-grid">
          <article class="operations-card">
            <div class="surface-heading">
              <div>
                <span class="panel-label">Workflow runs</span>
                <h3>AI and optimization</h3>
              </div>
              <span class="mini-badge"
                >{{ operationalActivity.workflowRuns.length }} items</span
              >
            </div>

            <ul class="detail-list">
              <li v-for="run in operationalActivity.workflowRuns" :key="run.id">
                <strong>{{ run.workflowKind }}</strong>
                <span
                  >{{ run.subjectLabel }} - {{ run.status }} -
                  {{ run.source }}</span
                >
                <span>{{ run.scenarioLabel }} - {{ run.createdAtLabel }}</span>
                <span>{{ run.summary }}</span>
              </li>
            </ul>
          </article>

          <article class="operations-card">
            <div class="surface-heading">
              <div>
                <span class="panel-label">Projection snapshots</span>
                <h3>Persisted read models</h3>
              </div>
              <span class="mini-badge"
                >{{
                  operationalActivity.projectionSnapshots.length
                }}
                items</span
              >
            </div>

            <ul class="detail-list">
              <li
                v-for="snapshot in operationalActivity.projectionSnapshots"
                :key="snapshot.id"
              >
                <strong>{{ snapshot.projectionName }}</strong>
                <span
                  >{{ snapshot.projectionKey }} - {{ snapshot.source }}</span
                >
                <span>{{ snapshot.capturedAtLabel }}</span>
                <span>{{ snapshot.summary }}</span>
              </li>
            </ul>
          </article>

          <article class="operations-card">
            <div class="surface-heading">
              <div>
                <span class="panel-label">Audit trail</span>
                <h3>Protected API activity</h3>
              </div>
              <span class="mini-badge"
                >{{ operationalActivity.auditEntries.length }} items</span
              >
            </div>

            <ul class="detail-list">
              <li
                v-for="entry in operationalActivity.auditEntries"
                :key="entry.id"
              >
                <strong>{{ entry.actionLabel }}</strong>
                <span
                  >Status {{ entry.statusCode }} -
                  {{ entry.occurredAtLabel }}</span
                >
                <span>Correlation {{ entry.correlationId }}</span>
                <span>{{ entry.summary }}</span>
              </li>
            </ul>
          </article>
        </div>
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
            <span class="mini-badge">{{
              providerCatalog.generatedAtLabel
            }}</span>
          </div>
        </div>

        <p v-if="providerCatalogErrorMessage" class="catalog-editor-note">
          {{ providerCatalogErrorMessage }}
        </p>

        <div class="catalog-grid">
          <article
            v-for="provider in providerCatalog.providers"
            :key="provider.providerId"
            class="catalog-card"
          >
            <div class="catalog-card-head">
              <div>
                <strong>{{ provider.providerName }}</strong>
                <p>{{ provider.domain }} - {{ provider.kind }}</p>
              </div>
              <span
                :class="[
                  'severity-chip',
                  `severity-${provider.healthStatus.toLowerCase()}`,
                ]"
              >
                {{ provider.healthStatus }}
              </span>
            </div>

            <p class="catalog-summary">{{ provider.summary }}</p>

            <div class="catalog-meta">
              <span>{{
                provider.configurationEnabled ? "Enabled" : "Disabled"
              }}</span>
              <span>{{ provider.configurationEnvironment }}</span>
              <span>{{ provider.configurationReadiness }}</span>
              <span>{{ provider.syncStatusLabel }}</span>
              <span>{{ provider.lastActivityLabel }}</span>
            </div>

            <div class="chip-row">
              <span
                v-for="capability in provider.capabilities"
                :key="capability"
                class="catalog-chip"
                >{{ capability }}</span
              >
            </div>

            <div class="catalog-block">
              <span class="panel-label">Configuration</span>
              <div class="chip-row">
                <span
                  v-for="field in provider.configuredFields"
                  :key="`${provider.providerId}-configured-${field}`"
                  class="catalog-chip"
                >
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

              <div
                v-if="provider.authMode !== 'none'"
                class="catalog-auth-posture"
              >
                <span class="panel-label">Auth posture</span>
                <div class="catalog-meta">
                  <span>Mode: {{ provider.authMode }}</span>
                  <span
                    :class="
                      provider.authConfigured
                        ? 'auth-badge-ok'
                        : 'auth-badge-missing'
                    "
                  >
                    {{
                      provider.authConfigured
                        ? "API key: configured"
                        : "API key: not set"
                    }}
                  </span>
                </div>
              </div>
            </div>

            <div class="catalog-block">
              <div class="catalog-editor-heading">
                <span class="panel-label">Runtime editor</span>
                <span class="catalog-editor-note"
                  >Changes persist to local appsettings.Local.json only.</span
                >
              </div>

              <label class="catalog-toggle">
                <input
                  v-model="providerDraft(provider.providerId).enabled"
                  type="checkbox"
                  :disabled="
                    !isProviderCatalogConnected ||
                    savingProviderId === provider.providerId
                  "
                />
                <span>Provider enabled</span>
              </label>

              <label class="catalog-field">
                <span>Environment</span>
                <input
                  v-model="providerDraft(provider.providerId).environment"
                  type="text"
                  :disabled="
                    !isProviderCatalogConnected ||
                    savingProviderId === provider.providerId
                  "
                />
              </label>

              <div class="catalog-editor-grid">
                <label
                  v-for="setting in provider.editableSettings"
                  :key="`${provider.providerId}-setting-${setting.key}`"
                  class="catalog-field"
                >
                  <span
                    >{{ setting.key
                    }}<small v-if="setting.required">Required</small></span
                  >
                  <input
                    v-model="
                      providerDraft(provider.providerId).settings[setting.key]
                    "
                    type="text"
                    :disabled="
                      !isProviderCatalogConnected ||
                      savingProviderId === provider.providerId
                    "
                  />
                </label>
              </div>

              <div class="catalog-editor-actions">
                <button
                  type="button"
                  class="catalog-save-button"
                  :disabled="
                    !isProviderCatalogConnected ||
                    savingProviderId === provider.providerId
                  "
                  @click="saveProviderConfiguration(provider)"
                >
                  {{
                    savingProviderId === provider.providerId
                      ? "Saving..."
                      : "Save local configuration"
                  }}
                </button>
                <span
                  v-if="providerConfigurationNotice[provider.providerId]"
                  class="catalog-editor-note"
                >
                  {{ providerConfigurationNotice[provider.providerId] }}
                </span>
              </div>
            </div>

            <div v-if="provider.authMode !== 'none'" class="catalog-block">
              <div class="catalog-editor-heading">
                <span class="panel-label">Secret key</span>
                <span class="catalog-editor-note">
                  {{
                    provider.authConfigured
                      ? "API key is set. Enter a new value to rotate it."
                      : "API key required for live upstream access."
                  }}
                  Stored in local secrets file only — never committed to source
                  control.
                </span>
              </div>

              <div class="catalog-secret-row">
                <input
                  v-model="secretKeyDrafts[provider.providerId]"
                  type="password"
                  autocomplete="new-password"
                  placeholder="Paste API key here"
                  :disabled="
                    !isProviderCatalogConnected ||
                    savingSecretProviderId === provider.providerId
                  "
                  class="catalog-secret-input"
                />
                <button
                  type="button"
                  class="catalog-save-button"
                  :disabled="
                    !isProviderCatalogConnected ||
                    savingSecretProviderId === provider.providerId ||
                    !secretKeyDrafts[provider.providerId]
                  "
                  @click="setProviderSecret(provider, 'apiKey')"
                >
                  {{
                    savingSecretProviderId === provider.providerId
                      ? "Saving..."
                      : "Set API key"
                  }}
                </button>
              </div>
              <span
                v-if="secretSaveNotice[provider.providerId]"
                class="catalog-editor-note"
              >
                {{ secretSaveNotice[provider.providerId] }}
              </span>
            </div>

            <div class="catalog-block">
              <span class="panel-label">Supported read models</span>
              <div class="chip-row">
                <span
                  v-for="readModel in provider.supportedReadModels"
                  :key="readModel"
                  class="catalog-chip is-muted"
                >
                  {{ readModel }}
                </span>
              </div>
            </div>

            <div class="catalog-block">
              <span class="panel-label">Schema</span>
              <p>
                {{ provider.schemaResourceName }} -
                {{ provider.schemaFields.length }} fields
              </p>
              <ul class="schema-list">
                <li
                  v-for="field in provider.schemaFields.slice(0, 4)"
                  :key="`${provider.providerId}-${field.name}`"
                >
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
  grid-template-columns: minmax(420px, 0.95fr) minmax(520px, 1.15fr);
  gap: 16px;
  align-items: start;
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
  grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
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

.ai-surface {
  margin-top: 16px;
}

.optimization-surface {
  margin-top: 16px;
}

.operations-surface {
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

.ai-workflow-grid {
  display: grid;
  grid-template-columns: minmax(0, 0.9fr) minmax(0, 1.1fr);
  gap: 14px;
}

.optimization-grid {
  display: grid;
  grid-template-columns: minmax(0, 0.8fr) minmax(0, 1fr) minmax(0, 1fr);
  gap: 14px;
}

.optimization-grid.is-detail {
  grid-template-columns: repeat(3, minmax(0, 1fr));
  margin-top: 14px;
}

.operations-summary-grid {
  display: grid;
  grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);
  gap: 14px;
}

.operations-grid {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 14px;
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

.transport-detail-grid {
  display: grid;
  gap: 12px;
}

.transport-sync-card {
  display: grid;
  gap: 14px;
  padding: 16px;
  border: 1px solid rgba(148, 163, 184, 0.14);
  border-radius: 8px;
  background: rgba(8, 17, 31, 0.55);
}

.transport-sync-card h3,
.transport-sync-delta p,
.transport-sync-metric strong,
.transport-sync-metric span {
  margin: 0;
}

.transport-sync-card h3,
.transport-sync-metric strong {
  color: #f8fafc;
}

.transport-sync-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
  gap: 12px;
}

.transport-sync-metric {
  display: grid;
  gap: 8px;
  min-width: 0;
  padding: 14px;
  border: 1px solid rgba(148, 163, 184, 0.12);
  border-radius: 8px;
  background: rgba(15, 23, 42, 0.82);
}

.transport-sync-metric span {
  color: #cbd5e1;
}

.transport-sync-delta {
  display: grid;
  gap: 8px;
  padding: 14px;
  border: 1px solid rgba(148, 163, 184, 0.12);
  border-radius: 8px;
  background: rgba(15, 23, 42, 0.82);
}

.transport-sync-delta p {
  color: #cbd5e1;
}

.transport-story-grid {
  display: grid;
  grid-template-columns: 1fr;
  gap: 12px;
}

.transport-support-grid {
  display: grid;
  grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);
  gap: 12px;
}

.transport-story-card {
  display: grid;
  gap: 12px;
  min-width: 0;
  padding: 16px;
  border: 1px solid rgba(148, 163, 184, 0.14);
  border-radius: 8px;
  background: rgba(8, 17, 31, 0.55);
}

.transport-story-card h3,
.transport-story-list strong,
.transport-story-list span {
  margin: 0;
}

.transport-story-card h3,
.transport-story-list strong {
  color: #f8fafc;
}

.transport-story-list {
  display: grid;
  gap: 10px;
  margin: 0;
  padding: 0;
  list-style: none;
}

.transport-story-list li {
  display: grid;
  gap: 4px;
  padding: 12px 14px;
  border: 1px solid rgba(148, 163, 184, 0.12);
  border-radius: 8px;
  background: rgba(15, 23, 42, 0.82);
}

.transport-story-list span {
  color: #cbd5e1;
}

.transport-support-actions {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(210px, 1fr));
  gap: 12px;
}

.transport-support-action {
  display: grid;
  gap: 6px;
  padding: 14px;
  text-align: left;
  color: #e2e8f0;
  border: 1px solid rgba(148, 163, 184, 0.16);
  border-radius: 8px;
  background: rgba(15, 23, 42, 0.82);
  cursor: pointer;
  transition: border-color 140ms ease, background 140ms ease,
    transform 140ms ease;
}

.transport-support-action strong,
.transport-support-action span {
  margin: 0;
}

.transport-support-action strong {
  color: #f8fafc;
}

.transport-support-action span {
  color: #cbd5e1;
}

.transport-support-action:hover:not(:disabled) {
  border-color: rgba(96, 165, 250, 0.4);
  background: rgba(30, 41, 59, 0.9);
  transform: translateY(-1px);
}

.transport-support-action:disabled {
  cursor: not-allowed;
  opacity: 0.62;
}

.transport-recovery-item {
  align-items: start;
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
  background: linear-gradient(
    180deg,
    rgba(13, 30, 50, 0.92) 0%,
    rgba(13, 23, 36, 0.98) 100%
  );
}

.map-grid {
  position: absolute;
  inset: 0;
  background-image: linear-gradient(rgba(51, 65, 85, 0.34) 1px, transparent 1px),
    linear-gradient(90deg, rgba(51, 65, 85, 0.34) 1px, transparent 1px);
  background-size: 48px 48px;
  mask-image: linear-gradient(
    180deg,
    rgba(255, 255, 255, 0.9),
    rgba(255, 255, 255, 0.4)
  );
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
.timeline-metric,
.route-detail-card {
  padding: 14px;
  border: 1px solid rgba(148, 163, 184, 0.14);
  border-radius: 8px;
  background: rgba(8, 17, 31, 0.55);
}

.route-row {
  display: flex;
  align-items: start;
  justify-content: space-between;
  gap: 12px;
  width: 100%;
  padding: 14px;
  appearance: none;
  text-align: left;
  cursor: pointer;
}

.route-row.is-selected {
  border-color: rgba(96, 165, 250, 0.46);
  background: rgba(37, 99, 235, 0.18);
}

.route-detail-card {
  display: grid;
  gap: 12px;
}

.route-detail-head {
  display: flex;
  align-items: start;
  justify-content: space-between;
  gap: 12px;
}

.route-detail-head h3 {
  margin: 4px 0 2px;
  font-size: 20px;
  color: #f8fafc;
}

.route-detail-head p,
.route-detail-meta,
.detail-list span {
  margin: 0;
  color: #cbd5e1;
}

.route-detail-meta {
  display: flex;
  flex-wrap: wrap;
  gap: 8px 10px;
  font-size: 13px;
}

.route-detail-meta span {
  padding: 6px 10px;
  border: 1px solid rgba(148, 163, 184, 0.14);
  border-radius: 999px;
  background: rgba(15, 23, 42, 0.72);
}

.transport-detail-columns {
  display: grid;
  grid-template-columns: 1fr;
  gap: 12px;
}

.transport-detail-block {
  display: grid;
  gap: 8px;
}

.detail-list {
  display: grid;
  gap: 8px;
  margin: 0;
  padding: 0;
  list-style: none;
}

.detail-list li {
  display: grid;
  gap: 2px;
  padding: 10px 12px;
  border: 1px solid rgba(148, 163, 184, 0.12);
  border-radius: 8px;
  background: rgba(15, 23, 42, 0.82);
}

.detail-list strong {
  color: #f8fafc;
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

.operations-card {
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

.catalog-field textarea {
  width: 100%;
  min-height: 96px;
  padding: 10px 12px;
  color: #e2e8f0;
  background: rgba(15, 23, 42, 0.92);
  border: 1px solid rgba(148, 163, 184, 0.2);
  border-radius: 8px;
  resize: vertical;
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
.catalog-field textarea:disabled,
.catalog-toggle input:disabled {
  cursor: not-allowed;
  opacity: 0.6;
}

.ai-prompt-chip {
  appearance: none;
  cursor: pointer;
  text-align: left;
}

.ai-query-panel,
.ai-answer-panel {
  display: grid;
  gap: 12px;
  min-width: 0;
  padding: 16px;
  border: 1px solid rgba(148, 163, 184, 0.14);
  border-radius: 8px;
  background: rgba(8, 17, 31, 0.55);
}

.optimization-summary-card,
.optimization-plan-card,
.optimization-detail-card {
  display: grid;
  gap: 12px;
  min-width: 0;
  padding: 16px;
  border: 1px solid rgba(148, 163, 184, 0.14);
  border-radius: 8px;
  background: rgba(8, 17, 31, 0.55);
}

.ai-answer-body {
  display: grid;
  gap: 14px;
}

.ai-context-meta {
  display: flex;
  flex-wrap: wrap;
  gap: 8px 10px;
}

.ai-context-meta span {
  padding: 6px 10px;
  color: #cbd5e1;
  font-size: 13px;
  border: 1px solid rgba(148, 163, 184, 0.14);
  border-radius: 999px;
  background: rgba(15, 23, 42, 0.72);
}

.optimization-order-list {
  display: grid;
  gap: 8px;
  margin: 0;
  padding-left: 18px;
  color: #e2e8f0;
}

.optimization-order-list li {
  padding: 10px 12px;
  border: 1px solid rgba(148, 163, 184, 0.12);
  border-radius: 8px;
  background: rgba(15, 23, 42, 0.82);
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

.catalog-auth-posture {
  display: grid;
  gap: 6px;
  margin-top: 4px;
}

.auth-badge-ok {
  color: #bbf7d0;
  font-size: 11px;
  font-weight: 500;
}

.auth-badge-missing {
  color: #fde68a;
  font-size: 11px;
  font-weight: 500;
}

.catalog-secret-row {
  display: flex;
  gap: 8px;
  align-items: center;
}

.catalog-secret-input {
  flex: 1;
  padding: 6px 10px;
  font-size: 12px;
  color: #e2e8f0;
  background: rgba(15, 23, 42, 0.6);
  border: 1px solid rgba(148, 163, 184, 0.22);
  border-radius: 5px;
  min-width: 0;
}

.catalog-secret-input:focus {
  outline: none;
  border-color: rgba(96, 165, 250, 0.5);
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
  .zone-strip,
  .ai-workflow-grid,
  .operations-summary-grid,
  .operations-grid,
  .optimization-grid,
  .optimization-grid.is-detail {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }

  .summary-strip {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }

  .catalog-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }

  .connection-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }

  .transport-detail-columns {
    grid-template-columns: 1fr;
  }

  .transport-sync-grid {
    grid-template-columns: 1fr;
  }

  .transport-story-grid {
    grid-template-columns: 1fr;
  }

  .transport-support-grid {
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
  .catalog-grid,
  .ai-workflow-grid,
  .operations-summary-grid,
  .operations-grid,
  .optimization-grid,
  .optimization-grid.is-detail {
    grid-template-columns: 1fr;
  }

  .connection-grid,
  .transport-detail-columns,
  .transport-sync-grid,
  .transport-story-grid {
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
