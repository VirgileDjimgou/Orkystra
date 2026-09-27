<!-- Prettier and the legacy Vue closing-bracket rule disagree on wrapped inline content. -->
<!-- eslint-disable vue/html-closing-bracket-newline, vue/html-indent -->
<template>
  <article
    class="operations-card"
    :class="{ 'operations-card-active': active }"
    tabindex="0"
    role="listitem"
    @focus="emit('focus', item.id)"
  >
    <div class="operations-card-main">
      <label class="form-check">
        <input
          :checked="selected"
          class="form-check-input"
          type="checkbox"
          :aria-label="`Select ${item.title}`"
          @change="emit('toggle', item.id)"
        />
      </label>
      <div class="operations-card-copy">
        <div class="d-flex flex-wrap gap-2 align-items-center">
          <strong>{{ item.title }}</strong>
          <span :class="severityBadgeClass(item.severity)">{{
            item.severity
          }}</span>
          <span :class="workflowBadgeClass(item.workflowStatus)">{{
            item.workflowStatus
          }}</span>
          <span class="badge text-bg-light">{{ item.sourceType }}</span>
        </div>
        <p class="mb-1">{{ item.message }}</p>
        <div class="operations-context">
          <span v-if="item.links.missionReference"
            >Mission {{ item.links.missionReference }}</span
          >
          <span v-if="item.links.vehicleRegistrationNumber"
            >Vehicle {{ item.links.vehicleRegistrationNumber }}</span
          >
          <span v-if="item.links.driverName"
            >Driver {{ item.links.driverName }}</span
          >
          <span v-if="item.assignedToDisplayName"
            >Owner {{ item.assignedToDisplayName }}</span
          >
          <span v-if="item.snoozedUntilUtc"
            >Snoozed until {{ formatDateTime(item.snoozedUntilUtc) }}</span
          >
        </div>
      </div>
    </div>
    <div class="operations-card-side">
      <small>{{ formatAge(item.detectedAtUtc) }}</small>
      <small>{{ formatDateTime(item.detectedAtUtc) }}</small>
    </div>
    <div class="operations-actions">
      <select
        :value="assignmentUserId"
        class="form-select form-select-sm action-select"
        aria-label="Assign owner"
        @change="
          emit(
            'update:assignmentUserId',
            ($event.target as HTMLSelectElement).value,
          )
        "
      >
        <option value="">Assign owner</option>
        <option
          v-for="assignee in assignees"
          :key="assignee.userId"
          :value="assignee.userId"
        >
          {{ assignee.fullName }}
        </option>
      </select>
      <button
        class="btn btn-outline-primary btn-sm"
        type="button"
        :disabled="!assignmentUserId"
        @click="emit('assign', item)"
      >
        Assign
      </button>
      <button
        class="btn btn-outline-success btn-sm"
        type="button"
        @click="emit('acknowledge', item)"
      >
        Acknowledge
      </button>
      <button
        class="btn btn-outline-warning btn-sm"
        type="button"
        @click="emit('snooze', item)"
      >
        Snooze
      </button>
      <button
        class="btn btn-outline-dark btn-sm"
        type="button"
        @click="emit('resolve', item)"
      >
        Resolve
      </button>
      <RouterLink
        v-if="item.links.alertId"
        class="btn btn-link btn-sm"
        to="/alerts"
      >
        Open alert flow
      </RouterLink>
      <RouterLink
        v-else-if="item.links.missionId"
        class="btn btn-link btn-sm"
        to="/dispatch/missions"
      >
        Open mission flow
      </RouterLink>
    </div>
  </article>
</template>

<script setup lang="ts">
import { RouterLink } from "vue-router";
import type { AlertAssigneeResponse } from "../alerts/contracts";
import type { OperationsExceptionListItemResponse } from "./contracts";

defineProps<{
  item: OperationsExceptionListItemResponse;
  assignees: AlertAssigneeResponse[];
  assignmentUserId: string;
  selected: boolean;
  active: boolean;
}>();
const emit = defineEmits<{
  focus: [id: string];
  toggle: [id: string];
  "update:assignmentUserId": [userId: string];
  assign: [item: OperationsExceptionListItemResponse];
  acknowledge: [item: OperationsExceptionListItemResponse];
  snooze: [item: OperationsExceptionListItemResponse];
  resolve: [item: OperationsExceptionListItemResponse];
}>();

function severityBadgeClass(severity: string) {
  return severity === "Critical"
    ? "badge text-bg-danger"
    : severity === "Warning"
      ? "badge text-bg-warning"
      : "badge text-bg-secondary";
}
function workflowBadgeClass(status: string) {
  return status === "Acknowledged"
    ? "badge text-bg-primary"
    : status === "Snoozed"
      ? "badge text-bg-warning"
      : "badge text-bg-secondary";
}
function formatDateTime(value: string) {
  return new Date(value).toLocaleString();
}
function formatAge(value: string) {
  const minutes = Math.max(
    1,
    Math.round((Date.now() - new Date(value).getTime()) / 60000),
  );
  return minutes < 60
    ? `${minutes} min ago`
    : minutes < 1440
      ? `${Math.round(minutes / 60)} h ago`
      : `${Math.round(minutes / 1440)} d ago`;
}
</script>

<style scoped>
.operations-card {
  display: grid;
  gap: 0.9rem;
  padding: 1rem;
  border: 1px solid rgba(15, 23, 42, 0.08);
  border-radius: 1rem;
  background: linear-gradient(180deg, rgba(247, 252, 251, 0.94), #fff), #fff;
}
.operations-card-active {
  border-color: rgba(13, 107, 93, 0.35);
  box-shadow: 0 16px 36px rgba(13, 107, 93, 0.08);
}
.operations-card-main {
  display: grid;
  grid-template-columns: auto minmax(0, 1fr);
  gap: 0.9rem;
  align-items: start;
}
.operations-card-copy {
  display: grid;
  gap: 0.45rem;
}
.operations-card-copy p,
.operations-context,
.operations-card-side {
  color: var(--muted);
}
.operations-context,
.operations-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 0.7rem;
  font-size: 0.9rem;
}
.operations-card-side {
  display: grid;
  justify-items: end;
  font-size: 0.84rem;
}
.operations-actions {
  align-items: center;
  gap: 0.55rem;
}
.action-select {
  max-width: 240px;
}
.operations-card:focus-visible {
  outline: 3px solid rgba(13, 107, 93, 0.35);
  outline-offset: 2px;
}
@media (max-width: 768px) {
  .operations-card-main {
    grid-template-columns: 1fr;
  }
  .operations-card-side {
    justify-items: start;
  }
}
</style>
