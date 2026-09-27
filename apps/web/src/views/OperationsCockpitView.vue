<template>
  <div class="cockpit-page">
    <section class="cockpit-toolbar surface-panel">
      <div>
        <span class="eyebrow">Live operations</span>
        <h1>Operations cockpit</h1>
        <p>Map-first fleet context for the work that needs attention now.</p>
      </div>
      <div class="cockpit-toolbar-actions">
        <CockpitContextLinks />
        <span :class="connectionBadgeClass">{{ connectionLabel }}</span>
        <button
          class="btn btn-outline-secondary"
          type="button"
          :disabled="isLoading"
          @click="refresh"
        >
          {{ isLoading ? "Refreshing..." : "Refresh" }}
        </button>
      </div>
    </section>

    <p v-if="pageError" class="alert alert-danger">{{ pageError }}</p>
    <div class="cockpit-workspace">
      <section class="cockpit-map-panel" aria-label="Fleet map workspace">
        <CockpitMapCanvas
          :positions="tracking.positions"
          :selected-vehicle-id="selection.vehicleId"
          :exception-vehicle-ids="exceptionVehicleIds"
          :trail-points="tracking.history?.items"
          @select="selectVehicle"
        />
        <div class="cockpit-kpis" aria-label="Fleet indicators">
          <span class="cockpit-kpi">
            <strong>{{
              tracking.metrics?.currentVehicleCount ?? tracking.positions.length
            }}</strong>
            <span>tracked</span>
          </span>
          <span class="cockpit-kpi">
            <strong>{{ queue.summary.totalActive }}</strong>
            <span>exceptions</span>
          </span>
          <span class="cockpit-kpi">
            <strong>{{ dispatch.missions.length }}</strong>
            <span>missions</span>
          </span>
        </div>
      </section>
      <CockpitInspector
        :vehicle="selectedVehicle"
        :diagnostic="selectedDiagnostic"
        :mission="selectedMission"
        :exception="selectedException"
      />
    </div>

    <CockpitActivityDock
      v-model:active-tab="activeTab"
      v-model:height="dockHeight"
      :collapsed="dockCollapsed"
      :exceptions="queue.items"
      :missions="dispatch.missions"
      :activities="agentActivities"
      @toggle="dockCollapsed = !dockCollapsed"
      @select-exception="selectException"
      @select-mission="selectMission"
    />
  </div>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, reactive, ref } from "vue";
import { useRoute, useRouter } from "vue-router";
import CockpitActivityDock from "../features/cockpit/CockpitActivityDock.vue";
import CockpitInspector from "../features/cockpit/CockpitInspector.vue";
import CockpitMapCanvas from "../features/cockpit/CockpitMapCanvas.vue";
import CockpitContextLinks from "../features/cockpit/CockpitContextLinks.vue";
import type {
  AgentActivityResponse,
  CockpitDockTab,
  CockpitSelection,
} from "../features/cockpit/contracts";
import { useSessionStore } from "../features/auth/store";
import { useDispatchStore } from "../features/dispatch/store";
import type { OperationsExceptionQueueResponse } from "../features/operations/contracts";
import { connectOperationsStream } from "../features/operations/live";
import {
  connectTrackingStream,
  type TrackingConnectionState,
} from "../features/tracking/live";
import { useTrackingStore } from "../features/tracking/store";
import { apiRequest } from "../services/api";

const session = useSessionStore();
const tracking = useTrackingStore();
const dispatch = useDispatchStore();
const route = useRoute();
const router = useRouter();
const selection = reactive<CockpitSelection>({
  vehicleId: null,
  missionId: null,
  exceptionId: null,
});
const queue = ref<OperationsExceptionQueueResponse>({
  summary: {
    totalActive: 0,
    criticalCount: 0,
    warningCount: 0,
    snoozedCount: 0,
    unassignedCount: 0,
  },
  items: [],
});
const agentActivities = ref<AgentActivityResponse[]>([]);
const activeTab = ref<CockpitDockTab>("exceptions");
const dockCollapsed = ref(false);
const dockHeight = ref(280);
const pageError = ref("");
const trackingState = ref<TrackingConnectionState>("idle");
let trackingConnection: { stop(): Promise<void> } | null = null;
let operationsConnection: { stop(): Promise<void> } | null = null;

const isLoading = computed(
  () =>
    tracking.positionsStatus === "loading" ||
    dispatch.missionsStatus === "loading",
);
const selectedVehicle = computed(
  () =>
    tracking.positions.find(
      (position) => position.vehicleId === selection.vehicleId,
    ) ?? null,
);
const selectedDiagnostic = computed(
  () =>
    tracking.diagnostics.find(
      (diagnostic) => diagnostic.vehicleId === selection.vehicleId,
    ) ?? null,
);
const selectedMission = computed(
  () =>
    dispatch.missions.find(
      (mission) =>
        mission.id === selection.missionId ||
        mission.vehicleId === selection.vehicleId,
    ) ?? null,
);
const selectedException = computed(
  () =>
    queue.value.items.find((item) => item.id === selection.exceptionId) ?? null,
);
const exceptionVehicleIds = computed(() =>
  queue.value.items
    .filter((item) => item.workflowStatus !== "Resolved")
    .map((item) => item.links.vehicleId)
    .filter((vehicleId): vehicleId is string => !!vehicleId),
);
const connectionLabel = computed(() =>
  trackingState.value === "live"
    ? "Live stream"
    : trackingState.value === "reconnecting"
      ? "Reconnecting"
      : trackingState.value === "offline"
        ? "Offline"
        : "Connecting",
);
const connectionBadgeClass = computed(() =>
  trackingState.value === "live"
    ? "badge text-bg-success"
    : trackingState.value === "reconnecting"
      ? "badge text-bg-warning"
      : "badge text-bg-secondary",
);

async function loadQueue() {
  if (!session.accessToken) return;
  queue.value = await apiRequest<OperationsExceptionQueueResponse>(
    "/api/v1/operations/exceptions",
    { token: session.accessToken },
  );
}

async function loadAgentActivities() {
  if (!session.accessToken) return;
  agentActivities.value = await apiRequest<AgentActivityResponse[]>(
    "/api/v1/demo/agent-activities?take=50",
    { token: session.accessToken },
  );
}

async function refresh() {
  if (!session.accessToken) return;
  pageError.value = "";
  try {
    await Promise.all([
      tracking.refresh(session.accessToken),
      dispatch.loadMissions(session.accessToken),
      loadQueue(),
      loadAgentActivities(),
    ]);
    const vehicleId =
      typeof route.query.vehicleId === "string" ? route.query.vehicleId : "";
    const missionRef =
      typeof route.query.missionRef === "string" ? route.query.missionRef : "";
    if (
      vehicleId &&
      tracking.positions.some((position) => position.vehicleId === vehicleId)
    )
      selectVehicle(vehicleId);
    else if (missionRef) {
      const mission = dispatch.missions.find(
        (item) => item.reference === missionRef,
      );
      if (mission) selectMission(mission.id);
    } else if (!selection.vehicleId && tracking.positions[0])
      selectVehicle(tracking.positions[0].vehicleId);
  } catch {
    pageError.value =
      "Unable to load cockpit data. Existing deep routes remain available.";
  }
}

function updateFocusQuery() {
  void router.replace({
    query: {
      ...route.query,
      vehicleId: selection.vehicleId ?? undefined,
      missionRef: selectedMission.value?.reference ?? undefined,
    },
  });
}
function selectVehicle(vehicleId: string) {
  selection.vehicleId = vehicleId;
  const mission = dispatch.missions.find(
    (item) => item.vehicleId === vehicleId,
  );
  selection.missionId = mission?.id ?? null;
  if (session.accessToken)
    void tracking.loadHistory(session.accessToken, vehicleId, 1, 40);
  updateFocusQuery();
}
function selectMission(missionId: string) {
  const mission = dispatch.missions.find((item) => item.id === missionId);
  selection.missionId = missionId;
  selection.vehicleId = mission?.vehicleId ?? null;
  if (session.accessToken && mission?.vehicleId)
    void tracking.loadHistory(session.accessToken, mission.vehicleId, 1, 40);
  activeTab.value = "timeline";
  updateFocusQuery();
}
function selectException(exceptionId: string) {
  const exception = queue.value.items.find((item) => item.id === exceptionId);
  selection.exceptionId = exceptionId;
  selection.vehicleId = exception?.links.vehicleId ?? selection.vehicleId;
  selection.missionId = exception?.links.missionId ?? selection.missionId;
  if (session.accessToken && selection.vehicleId)
    void tracking.loadHistory(session.accessToken, selection.vehicleId, 1, 40);
  activeTab.value = "exceptions";
  updateFocusQuery();
}

onMounted(async () => {
  await refresh();
  if (!session.accessToken) return;
  try {
    trackingConnection = await connectTrackingStream(
      session.accessToken,
      (position) => tracking.applyLivePosition(position),
      (state) => {
        trackingState.value = state;
      },
      refresh,
    );
    operationsConnection = await connectOperationsStream(
      async () =>
        Promise.all([loadQueue(), loadAgentActivities()]).then(() => undefined),
      () => undefined,
    );
  } catch {
    trackingState.value = "offline";
  }
});
onBeforeUnmount(async () => {
  await trackingConnection?.stop();
  await operationsConnection?.stop();
});
</script>

<style scoped>
.cockpit-page {
  display: grid;
  gap: 1rem;
}
.cockpit-toolbar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 1rem;
}
.cockpit-toolbar h1 {
  margin: 0.15rem 0;
}
.cockpit-toolbar p {
  margin: 0;
  color: var(--muted);
}
.cockpit-toolbar-actions {
  display: flex;
  gap: 0.75rem;
  align-items: center;
}
.cockpit-workspace {
  display: grid;
  grid-template-columns: minmax(0, 2.35fr) minmax(280px, 0.9fr);
  gap: 1rem;
}
.cockpit-map-panel {
  min-width: 0;
  padding: 0.75rem;
  border: 1px solid var(--border);
  border-radius: 1rem;
  background: var(--surface);
}
.cockpit-kpis {
  display: flex;
  gap: 1.25rem;
  flex-wrap: wrap;
  padding: 0.9rem 0.4rem 0.2rem;
  color: var(--muted);
}
.cockpit-kpi {
  display: grid;
  gap: 0.1rem;
}
.cockpit-kpis strong {
  color: var(--text);
  font-size: 1.2rem;
}
@media (max-width: 1100px) {
  .cockpit-workspace {
    grid-template-columns: 1fr;
  }
}
@media (max-width: 640px) {
  .cockpit-toolbar {
    align-items: flex-start;
    flex-direction: column;
  }
  .cockpit-toolbar-actions {
    width: 100%;
    justify-content: space-between;
  }
}
</style>
