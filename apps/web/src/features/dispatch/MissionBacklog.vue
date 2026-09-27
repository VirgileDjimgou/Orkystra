<!-- Prettier and the legacy Vue closing-bracket rule disagree on wrapped inline content. -->
<!-- eslint-disable vue/html-closing-bracket-newline, vue/html-indent -->
<template>
  <section class="surface-panel" aria-labelledby="mission-backlog-title">
    <div class="panel-heading">
      <div>
        <h2 id="mission-backlog-title">Mission backlog</h2>
        <p>Draft, active, delayed, and completed operations.</p>
      </div>
      <button
        class="btn btn-outline-secondary"
        type="button"
        :disabled="status === 'loading'"
        @click="emit('refresh')"
      >
        {{ status === "loading" ? "Refreshing..." : "Refresh" }}
      </button>
    </div>

    <div v-if="error" class="alert alert-danger" role="alert">{{ error }}</div>
    <div
      v-else-if="status === 'loading' && missions.length === 0"
      class="empty-placeholder"
      aria-live="polite"
    >
      Loading missions...
    </div>
    <div v-else-if="missions.length === 0" class="empty-placeholder">
      No mission planned yet for this organization.
    </div>
    <div v-else class="user-list" role="list">
      <button
        v-for="mission in missions"
        :key="mission.id"
        :class="[
          'user-card text-start w-100',
          selectedMissionId === mission.id ? 'selected-row' : '',
        ]"
        type="button"
        role="button"
        @click="emit('select', mission.id)"
      >
        <div>
          <strong>{{ mission.reference }}</strong>
          <div class="text-secondary small">{{ mission.title }}</div>
          <div class="text-secondary small">
            {{
              formatRange(mission.scheduledStartUtc, mission.scheduledEndUtc)
            }}
          </div>
          <div class="text-secondary small">
            {{ mission.stopCount }} stop(s)
            <span v-if="mission.driverName">{{
              ` · ${mission.driverName}`
            }}</span>
            <span v-if="mission.vehicleRegistrationNumber"
              >· {{ mission.vehicleRegistrationNumber }}</span
            >
          </div>
        </div>
        <div class="user-meta">
          <span :class="statusBadgeClass(mission.status)">{{
            mission.status
          }}</span>
          <small v-if="mission.simulatedDelayMinutes > 0"
            >+{{ mission.simulatedDelayMinutes }} min</small
          >
          <small v-if="hasMissionLocation(mission)">Live map link</small>
        </div>
      </button>
    </div>
  </section>
</template>

<script setup lang="ts">
import type { MissionStatus, MissionSummaryResponse } from "./contracts";

defineProps<{
  missions: MissionSummaryResponse[];
  selectedMissionId?: string;
  status: "idle" | "loading" | "success" | "error";
  error: string;
}>();
const emit = defineEmits<{ refresh: []; select: [missionId: string] }>();

function formatRange(start: string, end: string): string {
  return `${new Date(start).toLocaleString()} - ${new Date(end).toLocaleString()}`;
}

function statusBadgeClass(status: MissionStatus): string {
  switch (status) {
    case "Completed":
      return "badge text-bg-success";
    case "Delayed":
      return "badge text-bg-warning";
    case "Cancelled":
      return "badge text-bg-danger";
    case "EnRoute":
    case "Arrived":
      return "badge text-bg-primary";
    default:
      return "badge text-bg-secondary";
  }
}

function hasMissionLocation(mission: MissionSummaryResponse): boolean {
  return (
    mission.currentLatitude !== null &&
    mission.currentLongitude !== null &&
    !!mission.vehicleId
  );
}
</script>

<style scoped>
.user-card {
  border: 1px solid transparent;
}

.user-card:focus-visible {
  outline: 3px solid rgba(13, 110, 253, 0.35);
  outline-offset: 2px;
}
</style>
