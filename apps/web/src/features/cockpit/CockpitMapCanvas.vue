<template>
  <div class="cockpit-map-frame">
    <div ref="mapElement" class="cockpit-map" aria-label="Live fleet map" />
    <p class="map-legend" aria-label="Map legend">
      › moving · ■ stopped · ! exception · ring indicates tracking quality
    </p>
    <div v-if="positions.length === 0" class="cockpit-map-empty">
      No live telemetry is available yet.
    </div>
  </div>
</template>

<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref, watch } from "vue";
import L from "leaflet";
import type { TrackingPositionResponse } from "../tracking/contracts";
import type { TrackingHistoryItemResponse } from "../tracking/contracts";
import { getMapTileProviderConfiguration } from "../map/configuration";
import { markerSemantics } from "../map/markerSemantics";

const props = defineProps<{
  positions: TrackingPositionResponse[];
  selectedVehicleId: string | null;
  exceptionVehicleIds?: string[];
  trailPoints?: TrackingHistoryItemResponse[];
}>();
const emit = defineEmits<{ select: [vehicleId: string] }>();

const mapElement = ref<HTMLElement | null>(null);
const markers = new Map<string, L.CircleMarker>();
let map: L.Map | undefined;
let selectedTrail: L.Polyline | undefined;

function ensureMap() {
  if (map || !mapElement.value) return;
  map = L.map(mapElement.value).setView([48.4914, 9.2043], 11);
  if (import.meta.env.MODE !== "test") {
    const provider = getMapTileProviderConfiguration();
    L.tileLayer(provider.url, {
      maxZoom: provider.maxZoom,
      attribution: provider.attribution,
    }).addTo(map);
  }
}

function syncMarkers() {
  if (!map) return;
  const visible = new Set(
    props.positions.map((position) => position.vehicleId),
  );
  for (const [vehicleId, marker] of markers) {
    if (!visible.has(vehicleId)) {
      marker.remove();
      markers.delete(vehicleId);
    }
  }
  for (const position of props.positions) {
    let marker = markers.get(position.vehicleId);
    if (!marker) {
      marker = L.circleMarker([position.latitude, position.longitude], {
        radius: 8,
      }).addTo(map);
      marker.on("click", () => emit("select", position.vehicleId));
      markers.set(position.vehicleId, marker);
    }
    marker.setLatLng([position.latitude, position.longitude]);
    const semantics = markerSemantics(
      position,
      props.exceptionVehicleIds?.includes(position.vehicleId) ?? false,
    );
    marker.setStyle({
      color:
        position.vehicleId === props.selectedVehicleId
          ? "#0d6efd"
          : semantics.color,
      fillColor: semantics.color,
      fillOpacity: 0.9,
    });
    marker.bindPopup(`${semantics.symbol} ${semantics.label}`);
    marker.getElement?.()?.setAttribute("aria-label", semantics.label);
  }
}

function syncTrail() {
  selectedTrail?.remove();
  selectedTrail = undefined;
  if (!map || !props.trailPoints || props.trailPoints.length < 2) return;
  selectedTrail = L.polyline(
    props.trailPoints.map((point) => [point.latitude, point.longitude]),
    {
      color: "#0d6efd",
      weight: 4,
      opacity: 0.7,
    },
  ).addTo(map);
}

function focusSelectedVehicle() {
  const selected = props.positions.find(
    (position) => position.vehicleId === props.selectedVehicleId,
  );
  if (map && selected) map.panTo([selected.latitude, selected.longitude]);
}

onMounted(() => {
  ensureMap();
  syncMarkers();
  syncTrail();
});
watch(
  () => [
    props.positions,
    props.selectedVehicleId,
    props.exceptionVehicleIds,
    props.trailPoints,
  ],
  () => {
    syncMarkers();
    syncTrail();
    focusSelectedVehicle();
  },
  { deep: true },
);
onBeforeUnmount(() => map?.remove());
</script>

<style scoped>
.cockpit-map-frame {
  position: relative;
  min-height: 460px;
}
.cockpit-map {
  min-height: 460px;
  border-radius: 1rem;
  overflow: hidden;
}
.cockpit-map-empty {
  position: absolute;
  inset: 1rem;
  display: grid;
  place-items: center;
  border-radius: 1rem;
  background: rgba(248, 249, 250, 0.9);
  color: var(--muted);
}
@media (max-width: 768px) {
  .cockpit-map-frame,
  .cockpit-map {
    min-height: 360px;
  }
}
</style>
