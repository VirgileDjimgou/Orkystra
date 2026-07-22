<template>
  <div class="stacked-page">
    <section class="page-hero">
      <div>
        <span class="eyebrow">Live tracking</span>
        <h1>Fleet map</h1>
        <p>
          Current positions, replay protection, paged history, and live SignalR
          updates for the signed-in organization.
        </p>
      </div>
      <div class="d-flex flex-column gap-2 align-items-end">
        <span class="badge text-bg-dark">{{
          session.user?.organizationName
        }}</span>
        <span v-if="missionFocus" class="badge text-bg-light">
          Mission focus: {{ missionFocus }}
        </span>
        <span :class="connectionBadgeClass">{{ connectionLabel }}</span>
      </div>
    </section>

    <div class="row g-4">
      <div class="col-xl-8">
        <section class="surface-panel">
          <div class="panel-heading">
            <div>
              <h2>Live vehicle positions</h2>
              <p>Map and list stay synchronized as telemetry arrives.</p>
            </div>
            <button
              class="btn btn-outline-secondary"
              :disabled="tracking.positionsStatus === 'loading'"
              @click="refresh"
            >
              {{
                tracking.positionsStatus === "loading"
                  ? "Refreshing..."
                  : "Refresh"
              }}
            </button>
          </div>

          <div v-if="tracking.positionsError" class="alert alert-danger">
            {{ tracking.positionsError }}
          </div>
          <div v-else-if="isInitialLoading" class="empty-placeholder">
            Loading fleet positions...
          </div>
          <div v-else class="map-shell">
            <div
              ref="mapElement"
              class="map-canvas"
              aria-label="Fleet tracking map"
            />
            <div
              v-if="tracking.positions.length === 0"
              class="empty-placeholder overlay-empty"
            >
              No telemetry received yet. Start the simulator to populate the
              map.
            </div>
          </div>
        </section>

        <div class="row g-3 mt-1">
          <div class="col-md-4">
            <section class="surface-panel compact-panel">
              <span class="eyebrow">Tracked now</span>
              <strong class="metric-value">{{ trackedNow }}</strong>
            </section>
          </div>
          <div class="col-md-4">
            <section class="surface-panel compact-panel">
              <span class="eyebrow">Duplicates ignored</span>
              <strong class="metric-value">{{
                tracking.metrics?.duplicateCount ?? 0
              }}</strong>
            </section>
          </div>
          <div class="col-md-4">
            <section class="surface-panel compact-panel">
              <span class="eyebrow">Out-of-order stored</span>
              <strong class="metric-value">{{
                tracking.metrics?.outOfOrderCount ?? 0
              }}</strong>
            </section>
          </div>
        </div>
      </div>

      <div class="col-xl-4">
        <section class="surface-panel">
          <div class="panel-heading">
            <div>
              <h2>Live vehicles</h2>
              <p>
                {{ tracking.positions.length }} vehicle(s) currently visible.
              </p>
            </div>
          </div>

          <div v-if="showWaitingTelemetry" class="empty-placeholder">
            Waiting for telemetry.
          </div>
          <div v-else class="user-list">
            <article
              v-for="position in tracking.positions"
              :key="position.vehicleId"
              :class="[
                'user-card',
                selectedVehicleId === position.vehicleId ? 'selected-row' : '',
              ]"
              role="button"
              tabindex="0"
              @click="selectVehicle(position.vehicleId)"
            >
              <div>
                <strong>{{ position.registrationNumber }}</strong>
                <div class="text-secondary small">
                  {{ position.displayName }}
                </div>
                <div class="text-secondary small">
                  {{ formatCoordinates(position.latitude, position.longitude) }}
                </div>
                <span :class="qualityBadgeClass(position.qualityStatus)">{{
                  position.qualityStatus ?? "Fresh"
                }}</span>
              </div>
              <div class="user-meta">
                <span>{{ position.speedKph.toFixed(0) }} km/h</span>
                <small>{{ formatTime(position.recordedAtUtc) }}</small>
              </div>
            </article>
          </div>
        </section>

        <section class="surface-panel mt-4">
          <div v-if="selectedDiagnostic" class="diagnostic-card mb-3">
            <strong>Vehicle and device diagnostic</strong>
            <span>{{ selectedDiagnostic.reason }}</span>
            <small>
              Device {{ selectedDiagnostic.deviceId }} ·
              {{ selectedDiagnostic.source }} · accuracy
              {{ selectedDiagnostic.accuracyMeters?.toFixed(0) ?? "—" }} m
            </small>
          </div>
          <div class="panel-heading">
            <div>
              <h2>Tracking history</h2>
              <p>
                {{
                  selectedVehicle
                    ? `History for ${selectedVehicle.registrationNumber}`
                    : "Select a vehicle to inspect its telemetry trail."
                }}
              </p>
            </div>
          </div>

          <div v-if="tracking.historyError" class="alert alert-danger">
            {{ tracking.historyError }}
          </div>
          <div v-else-if="!selectedVehicle" class="empty-placeholder">
            No vehicle selected yet.
          </div>
          <div
            v-else-if="
              tracking.historyStatus === 'loading' && !tracking.history
            "
            class="empty-placeholder"
          >
            Loading history...
          </div>
          <div
            v-else-if="tracking.history && tracking.history.items.length === 0"
            class="empty-placeholder"
          >
            No persisted history yet for this vehicle.
          </div>
          <div v-else-if="tracking.history" class="history-stack">
            <article
              v-for="item in tracking.history.items"
              :key="item.eventId"
              class="history-card"
            >
              <strong>{{ formatTime(item.recordedAtUtc) }}</strong>
              <span>{{ item.speedKph.toFixed(0) }} km/h</span>
              <small>{{ item.eventId }}</small>
              <small>{{ item.deviceId }}</small>
            </article>
            <div class="d-flex justify-content-between align-items-center">
              <button
                class="btn btn-outline-secondary btn-sm"
                :disabled="!canGoPrevious"
                @click="changeHistoryPage(-1)"
              >
                Previous
              </button>
              <small>
                Page {{ tracking.history.page }} / {{ historyTotalPages }}
              </small>
              <button
                class="btn btn-outline-secondary btn-sm"
                :disabled="!canGoNext"
                @click="changeHistoryPage(1)"
              >
                Next
              </button>
            </div>
          </div>
        </section>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import {
  computed,
  nextTick,
  onBeforeUnmount,
  onMounted,
  ref,
  watch,
} from "vue";
import L from "leaflet";
import { useRoute } from "vue-router";
import { useSessionStore } from "../features/auth/store";
import type { TrackingPositionResponse } from "../features/tracking/contracts";
import { connectTrackingStream } from "../features/tracking/live";
import { useTrackingStore } from "../features/tracking/store";

const session = useSessionStore();
const tracking = useTrackingStore();
const route = useRoute();
const mapElement = ref<HTMLElement | null>(null);
const selectedVehicleId = ref<string>("");
let map: L.Map | undefined;
let connection: { stop(): Promise<void> } | undefined;
const markers = new Map<string, L.CircleMarker>();

const selectedVehicle = computed(
  () =>
    tracking.positions.find(
      (item) => item.vehicleId === selectedVehicleId.value,
    ) ?? null,
);
const selectedDiagnostic = computed(
  () =>
    tracking.diagnostics.find(
      (item) => item.vehicleId === selectedVehicleId.value,
    ) ?? null,
);
const historyTotalPages = computed(() => {
  if (!tracking.history) return 1;
  return Math.max(
    1,
    Math.ceil(tracking.history.totalCount / tracking.history.pageSize),
  );
});
const canGoPrevious = computed(
  () => !!tracking.history && tracking.history.page > 1,
);
const canGoNext = computed(
  () => !!tracking.history && tracking.history.page < historyTotalPages.value,
);
const isInitialLoading = computed(
  () =>
    tracking.positionsStatus === "loading" && tracking.positions.length === 0,
);
const trackedNow = computed(
  () => tracking.metrics?.currentVehicleCount ?? tracking.positions.length,
);
const showWaitingTelemetry = computed(
  () =>
    tracking.positions.length === 0 && tracking.positionsStatus !== "loading",
);
const connectionLabel = computed(() => {
  switch (tracking.connectionState) {
    case "connecting":
      return "Connecting";
    case "live":
      return "Live stream";
    case "reconnecting":
      return "Reconnecting";
    case "offline":
      return "Offline";
    default:
      return "Idle";
  }
});
const missionFocus = computed(() => {
  const missionRef = route?.query?.missionRef;
  return typeof missionRef === "string" ? missionRef : "";
});
const connectionBadgeClass = computed(() => {
  switch (tracking.connectionState) {
    case "live":
      return "badge text-bg-success";
    case "reconnecting":
      return "badge text-bg-warning";
    case "offline":
      return "badge text-bg-danger";
    default:
      return "badge text-bg-secondary";
  }
});

function formatCoordinates(latitude: number, longitude: number): string {
  return `${latitude.toFixed(4)}, ${longitude.toFixed(4)}`;
}

function formatTime(value: string): string {
  return new Date(value).toLocaleTimeString();
}

function ensureMap() {
  if (map || !mapElement.value) return;

  map = L.map(mapElement.value).setView([48.4914, 9.2043], 11);
  if (import.meta.env.MODE !== "test") {
    L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
      maxZoom: 19,
      attribution: "&copy; OpenStreetMap contributors",
    }).addTo(map);
  }
}

function fitMapToPositions() {
  if (!map || tracking.positions.length === 0) return;
  if (tracking.positions.length === 1) {
    const first = tracking.positions[0];
    map.setView([first.latitude, first.longitude], 12);
    return;
  }

  const bounds = L.latLngBounds(
    tracking.positions.map((position) => [
      position.latitude,
      position.longitude,
    ]),
  );
  map.fitBounds(bounds, { padding: [24, 24] });
}

function syncMarkers() {
  if (!map) return;

  const visibleIds = new Set(tracking.positions.map((item) => item.vehicleId));
  for (const [vehicleId, marker] of markers.entries()) {
    if (!visibleIds.has(vehicleId)) {
      marker.remove();
      markers.delete(vehicleId);
    }
  }

  for (const position of tracking.positions) {
    let marker = markers.get(position.vehicleId);
    if (!marker) {
      marker = L.circleMarker([position.latitude, position.longitude], {
        radius: 8,
      }).addTo(map);
      marker.on("click", () => {
        void selectVehicle(position.vehicleId);
      });
      markers.set(position.vehicleId, marker);
    }

    marker.setLatLng([position.latitude, position.longitude]);
    marker.setStyle({
      color:
        selectedVehicleId.value === position.vehicleId
          ? "#0d6efd"
          : qualityColor(position.qualityStatus),
      fillColor:
        selectedVehicleId.value === position.vehicleId
          ? "#8fb9ff"
          : qualityColor(position.qualityStatus),
      fillOpacity: 0.9,
    });
    marker.bindPopup(
      `${position.registrationNumber} · ${position.speedKph.toFixed(0)} km/h`,
    );
  }
}

function qualityColor(status?: TrackingPositionResponse["qualityStatus"]) {
  return (
    {
      Fresh: "#198754",
      Delayed: "#fd7e14",
      Inaccurate: "#ffc107",
      Invalid: "#dc3545",
      Silent: "#6c757d",
    } as const
  )[status ?? "Fresh"];
}

function qualityBadgeClass(status?: TrackingPositionResponse["qualityStatus"]) {
  const tone =
    status === "Fresh"
      ? "text-bg-success"
      : status === "Delayed" || status === "Inaccurate"
        ? "text-bg-warning"
        : "text-bg-secondary";
  return `badge ${tone}`;
}

async function refresh() {
  if (!session.accessToken) return;
  await tracking.refresh(session.accessToken);
  await nextTick();
  ensureMap();
  syncMarkers();
  fitMapToPositions();

  const requestedVehicleId =
    typeof route?.query?.vehicleId === "string" ? route.query.vehicleId : "";

  if (
    requestedVehicleId &&
    tracking.positions.some((item) => item.vehicleId === requestedVehicleId)
  ) {
    await selectVehicle(requestedVehicleId, false);
    return;
  }

  if (!selectedVehicleId.value && tracking.positions.length > 0) {
    await selectVehicle(tracking.positions[0].vehicleId, false);
  }
}

async function selectVehicle(vehicleId: string, pan = true) {
  selectedVehicleId.value = vehicleId;
  syncMarkers();

  const vehicle = tracking.positions.find(
    (item) => item.vehicleId === vehicleId,
  );
  if (pan && map && vehicle) {
    map.panTo([vehicle.latitude, vehicle.longitude]);
  }

  if (session.accessToken) {
    await tracking.loadHistory(session.accessToken, vehicleId, 1, 5);
  }
}

async function changeHistoryPage(direction: number) {
  if (!session.accessToken || !selectedVehicleId.value || !tracking.history)
    return;
  const nextPage = tracking.history.page + direction;
  await tracking.loadHistory(
    session.accessToken,
    selectedVehicleId.value,
    nextPage,
    5,
  );
}

onMounted(async () => {
  ensureMap();
  await refresh();

  if (!session.accessToken) return;
  try {
    connection = await connectTrackingStream(
      session.accessToken,
      (position: TrackingPositionResponse) => {
        tracking.applyLivePosition(position);
        syncMarkers();
      },
      (state) => {
        tracking.setConnectionState(state);
      },
      async () => refresh(),
    );
  } catch {
    tracking.setConnectionState("offline");
    tracking.positionsError =
      "Live stream is currently unavailable. Manual refresh still works.";
  }
});

watch(
  () => [
    tracking.positions.map((item) => item.vehicleId).join(","),
    selectedVehicleId.value,
  ],
  () => {
    syncMarkers();
  },
);

onBeforeUnmount(async () => {
  await connection?.stop();
  map?.remove();
});
</script>

<style scoped>
.map-shell {
  position: relative;
}

.overlay-empty {
  position: absolute;
  inset: 1rem;
  background: rgba(248, 249, 250, 0.92);
  border-radius: 1rem;
  display: grid;
  place-items: center;
  text-align: center;
}

.compact-panel {
  min-height: 124px;
  justify-content: center;
}

.metric-value {
  display: block;
  font-size: 2rem;
  line-height: 1;
}

.selected-row {
  border-color: rgba(13, 110, 253, 0.35);
  box-shadow: 0 0 0 1px rgba(13, 110, 253, 0.15);
}

.history-stack {
  display: grid;
  gap: 0.75rem;
}

.history-card {
  display: grid;
  gap: 0.15rem;
  padding: 0.85rem 1rem;
  border-radius: 1rem;
  background: linear-gradient(180deg, rgba(248, 249, 250, 0.96), #ffffff);
  border: 1px solid rgba(15, 23, 42, 0.08);
}

.diagnostic-card {
  display: grid;
  gap: 0.35rem;
  padding: 0.85rem;
  border-left: 4px solid #0d6efd;
  background: #f8f9fa;
}
</style>
