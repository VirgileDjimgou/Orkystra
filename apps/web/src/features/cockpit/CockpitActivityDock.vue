<template>
  <section
    class="cockpit-dock"
    :class="{ collapsed }"
    :style="{ '--cockpit-dock-height': `${height}px` }"
  >
    <div class="cockpit-dock-heading">
      <div>
        <span class="eyebrow">Activity dock</span>
        <h2>{{ activeTabLabel }}</h2>
      </div>
      <button
        class="btn btn-outline-secondary btn-sm"
        type="button"
        :aria-expanded="!collapsed"
        @click="$emit('toggle')"
      >
        {{ collapsed ? "Expand activity" : "Collapse activity" }}
      </button>
    </div>
    <div v-if="!collapsed">
      <label class="dock-size-control">
        Activity height
        <input
          :value="height"
          type="range"
          min="180"
          max="460"
          step="20"
          @input="updateHeight"
        />
      </label>
      <div class="cockpit-tabs" role="tablist" aria-label="Cockpit activity">
        <button
          v-for="tab in tabs"
          :key="tab.id"
          type="button"
          role="tab"
          :aria-selected="activeTab === tab.id"
          :class="{ active: activeTab === tab.id }"
          @click="$emit('update:activeTab', tab.id)"
        >
          {{ tab.label }}
        </button>
      </div>
      <div v-if="activeTab === 'exceptions'" class="cockpit-list">
        <button
          v-for="item in exceptions.slice(0, 4)"
          :key="item.id"
          type="button"
          class="cockpit-list-item"
          @click="$emit('selectException', item.id)"
        >
          <strong>{{ item.title }}</strong>
          <span>{{ exceptionContext(item) }}</span>
        </button>
        <p v-if="exceptions.length === 0" class="empty-placeholder">
          No active exceptions.
        </p>
      </div>
      <div v-else-if="activeTab === 'missions'" class="cockpit-list">
        <button
          v-for="mission in missions.slice(0, 4)"
          :key="mission.id"
          type="button"
          class="cockpit-list-item"
          @click="$emit('selectMission', mission.id)"
        >
          <strong>{{ mission.reference }}</strong>
          <span>{{ missionContext(mission) }}</span>
        </button>
        <p v-if="missions.length === 0" class="empty-placeholder">
          No missions to show.
        </p>
      </div>
      <div v-else class="cockpit-list">
        <p class="empty-placeholder">
          Select a mission or exception to view its operational timeline.
        </p>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { computed } from "vue";
import type { CockpitDockTab } from "./contracts";
import type { MissionSummaryResponse } from "../dispatch/contracts";
import type { OperationsExceptionListItemResponse } from "../operations/contracts";

const props = defineProps<{
  activeTab: CockpitDockTab;
  collapsed: boolean;
  height: number;
  exceptions: OperationsExceptionListItemResponse[];
  missions: MissionSummaryResponse[];
}>();
const emit = defineEmits<{
  toggle: [];
  "update:activeTab": [tab: CockpitDockTab];
  "update:height": [height: number];
  selectException: [id: string];
  selectMission: [id: string];
}>();
const tabs: Array<{ id: CockpitDockTab; label: string }> = [
  { id: "exceptions", label: "Exceptions" },
  { id: "missions", label: "Missions" },
  { id: "timeline", label: "Timeline" },
];
const activeTabLabel = computed(
  () => tabs.find((tab) => tab.id === props.activeTab)?.label ?? "Activity",
);
function updateHeight(event: Event) {
  emit("update:height", Number((event.target as HTMLInputElement).value));
}
function exceptionContext(item: OperationsExceptionListItemResponse) {
  return `${item.links.vehicleRegistrationNumber ?? item.sourceType} · ${item.severity}`;
}
function missionContext(mission: MissionSummaryResponse) {
  return `${mission.status} · ${mission.vehicleRegistrationNumber ?? "Unassigned"}`;
}
</script>

<style scoped>
.cockpit-dock {
  display: grid;
  gap: 1rem;
  padding: 1rem 1.25rem;
  border: 1px solid var(--border);
  border-radius: 1rem;
  background: var(--surface);
  max-height: var(--cockpit-dock-height);
  overflow: auto;
}
.cockpit-dock-heading {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 1rem;
}
.cockpit-dock h2 {
  margin: 0;
  font-size: 1.1rem;
}
.cockpit-tabs {
  display: flex;
  gap: 0.4rem;
  border-bottom: 1px solid var(--border);
}
.dock-size-control {
  display: flex;
  align-items: center;
  gap: 0.65rem;
  color: var(--muted);
  font-size: 0.85rem;
}
.dock-size-control input {
  width: 10rem;
}
.cockpit-tabs button {
  border: 0;
  border-bottom: 2px solid transparent;
  background: transparent;
  padding: 0.45rem 0.65rem;
  color: var(--muted);
}
.cockpit-tabs button.active {
  border-color: var(--accent);
  color: var(--text);
  font-weight: 700;
}
.cockpit-list {
  display: grid;
  gap: 0.5rem;
}
.cockpit-list-item {
  display: grid;
  gap: 0.2rem;
  width: 100%;
  text-align: left;
  padding: 0.7rem;
  border: 1px solid var(--border);
  border-radius: 0.7rem;
  background: #fff;
}
.cockpit-list-item span {
  color: var(--muted);
  font-size: 0.88rem;
}
</style>
