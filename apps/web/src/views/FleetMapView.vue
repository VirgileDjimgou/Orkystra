<template>
  <div>
    <div class="mb-3">
      <h1 class="h3 mb-1">Carte flotte</h1>
      <p class="text-secondary mb-0">
        Positions reçues du simulateur de développement.
      </p>
    </div>
    <div v-if="error" class="alert alert-danger">{{ error }}</div>
    <div class="map-layout">
      <div
        ref="mapElement"
        class="map-canvas"
        aria-label="Carte des véhicules"
      ></div>
      <aside class="vehicle-panel">
        <h2 class="h6">Véhicules ({{ positions.length }})</h2>
        <div v-if="positions.length === 0" class="empty-state">
          Aucune position reçue.
        </div>
        <article
          v-for="position in positions"
          :key="position.vehicleId"
          class="vehicle-row"
        >
          <strong>{{ position.deviceId }}</strong>
          <span>{{ position.speedKph.toFixed(0) }} km/h</span>
          <small>{{
            new Date(position.recordedAtUtc).toLocaleTimeString()
          }}</small>
        </article>
      </aside>
    </div>
  </div>
</template>

<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref } from "vue";
import L from "leaflet";
import * as signalR from "@microsoft/signalr";

type Telemetry = {
  vehicleId: string;
  deviceId: string;
  recordedAtUtc: string;
  latitude: number;
  longitude: number;
  speedKph: number;
};

const mapElement = ref<HTMLElement | null>(null);
const positions = ref<Telemetry[]>([]);
const error = ref("");
let map: L.Map | undefined;
let connection: signalR.HubConnection | undefined;
const markers = new Map<string, L.CircleMarker>();

function updatePosition(point: Telemetry) {
  const index = positions.value.findIndex(
    (x) => x.vehicleId === point.vehicleId,
  );
  if (index >= 0) positions.value[index] = point;
  else positions.value.push(point);

  if (!map) return;
  const marker = markers.get(point.vehicleId);
  if (marker) marker.setLatLng([point.latitude, point.longitude]);
  else
    markers.set(
      point.vehicleId,
      L.circleMarker([point.latitude, point.longitude], { radius: 8 }).addTo(
        map,
      ),
    );
}

onMounted(async () => {
  if (!mapElement.value) return;
  map = L.map(mapElement.value).setView([48.4914, 9.2043], 12);
  L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
    maxZoom: 19,
    attribution: "&copy; OpenStreetMap contributors",
  }).addTo(map);

  try {
    const response = await fetch("/api/tracking/latest");
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    ((await response.json()) as Telemetry[]).forEach(updatePosition);

    connection = new signalR.HubConnectionBuilder()
      .withUrl("/hubs/tracking")
      .withAutomaticReconnect()
      .build();
    connection.on("telemetryUpdated", updatePosition);
    await connection.start();
  } catch (cause) {
    error.value =
      cause instanceof Error ? cause.message : "Connexion impossible.";
  }
});

onBeforeUnmount(async () => {
  await connection?.stop();
  map?.remove();
});
</script>
