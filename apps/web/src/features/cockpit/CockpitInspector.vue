<template>
  <aside class="cockpit-inspector" aria-live="polite">
    <header>
      <span class="eyebrow">Context inspector</span>
      <h2>{{ vehicle ? vehicle.registrationNumber : "No selection" }}</h2>
    </header>
    <div v-if="!vehicle" class="empty-placeholder">
      Select a vehicle or an exception to inspect its context.
    </div>
    <template v-else>
      <dl>
        <div>
          <dt>Vehicle</dt>
          <dd>{{ vehicle.displayName }}</dd>
        </div>
        <div>
          <dt>Speed</dt>
          <dd>{{ vehicle.speedKph.toFixed(0) }} km/h</dd>
        </div>
        <div>
          <dt>Quality</dt>
          <dd>{{ vehicle.qualityStatus ?? "Fresh" }}</dd>
        </div>
        <div>
          <dt>Device</dt>
          <dd>{{ diagnostic?.deviceId ?? vehicle.deviceId }}</dd>
        </div>
      </dl>
      <section v-if="mission" class="inspector-section">
        <span class="eyebrow">Mission</span>
        <strong>{{ mission.reference }}</strong>
        <span>{{ mission.title }} · {{ mission.status }}</span>
        <span v-if="mission.driverName">Driver: {{ mission.driverName }}</span>
      </section>
      <section v-if="exception" class="inspector-section inspector-exception">
        <span class="eyebrow">Exception</span>
        <strong>{{ exception.title }}</strong>
        <span>{{ exception.message }}</span>
      </section>
    </template>
  </aside>
</template>

<script setup lang="ts">
import type { MissionSummaryResponse } from "../dispatch/contracts";
import type { OperationsExceptionListItemResponse } from "../operations/contracts";
import type {
  TrackingDiagnosticResponse,
  TrackingPositionResponse,
} from "../tracking/contracts";

defineProps<{
  vehicle: TrackingPositionResponse | null;
  diagnostic: TrackingDiagnosticResponse | null;
  mission: MissionSummaryResponse | null;
  exception: OperationsExceptionListItemResponse | null;
}>();
</script>

<style scoped>
.cockpit-inspector {
  display: grid;
  align-content: start;
  gap: 1rem;
  height: 100%;
  padding: 1.25rem;
  border: 1px solid var(--border);
  border-radius: 1rem;
  background: var(--surface);
}
.cockpit-inspector header {
  display: grid;
  gap: 0.25rem;
}
.cockpit-inspector h2 {
  margin: 0;
  font-size: 1.25rem;
}
dl {
  display: grid;
  gap: 0.7rem;
  margin: 0;
}
dl div {
  display: flex;
  justify-content: space-between;
  gap: 1rem;
  border-bottom: 1px solid var(--border);
  padding-bottom: 0.55rem;
}
dt {
  color: var(--muted);
}
dd {
  margin: 0;
  text-align: right;
  font-weight: 650;
}
.inspector-section {
  display: grid;
  gap: 0.35rem;
  padding: 0.85rem;
  border-radius: 0.8rem;
  background: #f4f8f7;
}
.inspector-exception {
  border-left: 3px solid #fd7e14;
}
</style>
