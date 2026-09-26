<template>
  <div class="cockpit-map-frame">
    <div ref="mapElement" class="cockpit-map" aria-label="Live fleet map" />
    <div v-if="positions.length === 0" class="cockpit-map-empty">
      No live telemetry is available yet.
    </div>
  </div>
</template>

<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref, watch } from "vue";
import L from "leaflet";
import type { TrackingPositionResponse } from "../tracking/contracts";

const props = defineProps<{
  positions: TrackingPositionResponse[];
  selectedVehicleId: string | null;
}>();
const emit = defineEmits<{ select: [vehicleId: string] }>();

const mapElement = ref<HTMLElement | null>(null);
const markers = new Map<string, L.CircleMarker>();
let map: L.Map | undefined;

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

function markerColor(position: TrackingPositionResponse) {
  if (position.vehicleId === props.selectedVehicleId) return "#0d6efd";
  return position.qualityStatus === "Invalid" ? "#dc3545" : "#198754";
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
    marker.setStyle({
      color: markerColor(position),
      fillColor: markerColor(position),
      fillOpacity: 0.9,
    });
    marker.bindPopup(
      `${position.registrationNumber} · ${position.speedKph.toFixed(0)} km/h`,
    );
  }
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
});
watch(
  () => [props.positions, props.selectedVehicleId],
  () => {
    syncMarkers();
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
