<template>
  <div class="stacked-page">
    <section class="page-hero operations-hero">
      <div>
        <span class="eyebrow">Sprint 13</span>
        <h1>Operations center</h1>
        <p>
          Start from one exception queue, prioritize by impact, and resolve the
          fleet backlog without jumping across multiple screens.
        </p>
      </div>
      <div class="operations-live-rail">
        <span class="badge text-bg-dark">{{
          session.user?.organizationName
        }}</span>
        <span :class="connectionBadgeClass">{{ connectionLabel }}</span>
      </div>
    </section>

    <div class="row g-3">
      <div class="col-sm-6 col-xl-3">
        <div class="metric-card">
          <span class="text-secondary">Active exceptions</span>
          <strong class="display-6">{{ queue.summary.totalActive }}</strong>
          <small>Tenant-safe actionable backlog</small>
        </div>
      </div>
      <div class="col-sm-6 col-xl-3">
        <div class="metric-card">
          <span class="text-secondary">Critical</span>
          <strong class="display-6">{{ queue.summary.criticalCount }}</strong>
          <small>Immediate operator attention</small>
        </div>
      </div>
      <div class="col-sm-6 col-xl-3">
        <div class="metric-card">
          <span class="text-secondary">Unassigned</span>
          <strong class="display-6">{{ queue.summary.unassignedCount }}</strong>
          <small>Needs clear ownership</small>
        </div>
      </div>
      <div class="col-sm-6 col-xl-3">
        <div class="metric-card">
          <span class="text-secondary">Snoozed</span>
          <strong class="display-6">{{ queue.summary.snoozedCount }}</strong>
          <small>Temporarily deferred with reason</small>
        </div>
      </div>
    </div>

    <section class="surface-panel">
      <div class="panel-heading">
        <div>
          <h2>Queue controls</h2>
          <p>
            Search globally, combine filters, reuse saved views, and act safely
            in bulk.
          </p>
        </div>
        <div class="d-flex gap-2 flex-wrap justify-content-end">
          <button
            class="btn btn-outline-secondary"
            type="button"
            @click="refreshAll"
          >
            Refresh
          </button>
          <button
            class="btn btn-primary"
            type="button"
            @click="saveCurrentView"
          >
            Save current view
          </button>
        </div>
      </div>

      <div class="operations-toolbar">
        <input
          v-model="filters.search"
          class="form-control"
          placeholder="Search mission, driver, vehicle, alert, or sync issue"
          @keydown.enter.prevent="refreshQueue"
        />
        <select v-model="filters.sourceType" class="form-select">
          <option value="">All sources</option>
          <option value="Alert">Alerts</option>
          <option value="MissionDelay">Mission delays</option>
          <option value="CriticalDefect">Critical defects</option>
          <option value="DriverSync">Blocked sync</option>
        </select>
        <select v-model="filters.severity" class="form-select">
          <option value="">All severities</option>
          <option value="Critical">Critical</option>
          <option value="Warning">Warning</option>
          <option value="Info">Info</option>
        </select>
        <select v-model="filters.workflowStatus" class="form-select">
          <option value="">All workflow states</option>
          <option value="Open">Open</option>
          <option value="Acknowledged">Acknowledged</option>
          <option value="Snoozed">Snoozed</option>
        </select>
        <select v-model="filters.assignedToUserId" class="form-select">
          <option value="">All owners</option>
          <option
            v-for="assignee in assignees"
            :key="assignee.userId"
            :value="assignee.userId"
          >
            {{ assignee.fullName }}
          </option>
        </select>
        <label class="form-check operations-toggle">
          <input
            v-model="filters.includeSnoozed"
            class="form-check-input"
            type="checkbox"
          />
          <span class="form-check-label">Include snoozed</span>
        </label>
      </div>

      <div class="d-flex gap-2 flex-wrap align-items-center mt-3">
        <button
          v-for="view in savedViews"
          :key="view.id"
          class="btn btn-outline-dark btn-sm"
          type="button"
          @click="applySavedView(view)"
        >
          {{ view.name }}<span v-if="view.isShared"> · team</span>
        </button>
      </div>

      <div v-if="pageError" class="alert alert-danger mt-3 mb-0">
        {{ pageError }}
      </div>
      <div v-if="actionMessage" class="alert alert-success mt-3 mb-0">
        {{ actionMessage }}
      </div>
    </section>

    <div class="row g-4">
      <div class="col-12 col-xxl-8">
        <section class="surface-panel">
          <div class="panel-heading">
            <div>
              <h2>Unified exception queue</h2>
              <p>
                Critical-first ordering with optimistic concurrency on every
                action.
              </p>
            </div>
            <div class="bulk-actions">
              <select v-model="bulkAction" class="form-select form-select-sm">
                <option value="">Bulk action</option>
                <option value="acknowledge">Acknowledge</option>
                <option value="resolve">Resolve</option>
                <option value="snooze">Snooze 2h</option>
              </select>
              <button
                class="btn btn-outline-primary btn-sm"
                type="button"
                :disabled="selectedCount === 0 || !bulkAction"
                @click="runBulkAction"
              >
                Apply
              </button>
            </div>
          </div>

          <div v-if="status === 'loading'" class="empty-placeholder">
            Loading queue...
          </div>
          <div v-else-if="queue.items.length === 0" class="empty-placeholder">
            No active exceptions match the current filters.
          </div>
          <div v-else class="operations-queue" role="list">
            <article
              v-for="item in queue.items"
              :key="item.id"
              class="operations-card"
              :class="{ 'operations-card-active': focusedId === item.id }"
              tabindex="0"
              role="listitem"
              @focus="focusedId = item.id"
            >
              <div class="operations-card-main">
                <label class="form-check">
                  <input
                    :checked="selectedIds.has(item.id)"
                    class="form-check-input"
                    type="checkbox"
                    @change="toggleSelection(item.id)"
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
                    <span class="badge text-bg-light">{{
                      item.sourceType
                    }}</span>
                  </div>
                  <p class="mb-1">{{ item.message }}</p>
                  <div class="operations-context">
                    <!-- prettier-ignore -->
                    <span v-if="item.links.missionReference">Mission {{ item.links.missionReference }}</span>
                    <!-- prettier-ignore -->
                    <span v-if="item.links.vehicleRegistrationNumber">Vehicle {{ item.links.vehicleRegistrationNumber }}</span>
                    <!-- prettier-ignore -->
                    <span v-if="item.links.driverName">Driver {{ item.links.driverName }}</span>
                    <!-- prettier-ignore -->
                    <span v-if="item.assignedToDisplayName">Owner {{ item.assignedToDisplayName }}</span>
                    <!-- prettier-ignore -->
                    <span v-if="item.snoozedUntilUtc">Snoozed until {{ formatDateTime(item.snoozedUntilUtc) }}</span>
                  </div>
                </div>
              </div>

              <div class="operations-card-side">
                <small>{{ formatAge(item.detectedAtUtc) }}</small>
                <small>{{ formatDateTime(item.detectedAtUtc) }}</small>
              </div>

              <div class="operations-actions">
                <select
                  v-model="assignmentSelections[item.id]"
                  class="form-select form-select-sm action-select"
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
                  :disabled="!assignmentSelections[item.id]"
                  @click="assign(item)"
                >
                  Assign
                </button>
                <button
                  class="btn btn-outline-success btn-sm"
                  type="button"
                  @click="acknowledge(item)"
                >
                  Acknowledge
                </button>
                <button
                  class="btn btn-outline-warning btn-sm"
                  type="button"
                  @click="snooze(item)"
                >
                  Snooze
                </button>
                <button
                  class="btn btn-outline-dark btn-sm"
                  type="button"
                  @click="resolve(item)"
                >
                  Resolve
                </button>
                <RouterLink
                  v-if="item.links.alertId"
                  class="btn btn-link btn-sm"
                  :to="`/alerts`"
                >
                  Open alert flow
                </RouterLink>
                <RouterLink
                  v-else-if="item.links.missionId"
                  class="btn btn-link btn-sm"
                  :to="`/dispatch/missions`"
                >
                  Open mission flow
                </RouterLink>
              </div>
            </article>
          </div>
        </section>
      </div>

      <div class="col-12 col-xxl-4">
        <section class="surface-panel">
          <div class="panel-heading">
            <div>
              <h2>Active focus</h2>
              <p>Context for the currently focused exception.</p>
            </div>
          </div>

          <div v-if="!focusedItem" class="empty-placeholder">
            Focus an exception card to inspect its linked context.
          </div>
          <div v-else class="history-stack">
            <article class="history-card">
              <strong>{{ focusedItem.title }}</strong>
              <span>{{ focusedItem.message }}</span>
              <!-- prettier-ignore -->
              <small>{{ focusedItem.sourceType }} · {{ focusedItem.workflowStatus }}</small>
            </article>
            <article
              v-if="focusedItem.links.missionReference"
              class="history-card"
            >
              <strong>Mission</strong>
              <span>{{ focusedItem.links.missionReference }}</span>
              <small v-if="focusedItem.links.vehicleRegistrationNumber">
                Vehicle {{ focusedItem.links.vehicleRegistrationNumber }}
              </small>
              <small v-if="focusedItem.links.driverName">
                Driver {{ focusedItem.links.driverName }}
              </small>
            </article>
            <article v-if="focusedItem.snoozeReason" class="history-card">
              <strong>Snooze reason</strong>
              <span>{{ focusedItem.snoozeReason }}</span>
            </article>
            <article v-if="focusedItem.resolutionReason" class="history-card">
              <strong>Resolution reason</strong>
              <span>{{ focusedItem.resolutionReason }}</span>
            </article>
          </div>
        </section>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, reactive, ref } from "vue";
import { RouterLink } from "vue-router";
import { useSessionStore } from "../features/auth/store";
import { apiRequest } from "../services/api";
import type { AlertAssigneeResponse } from "../features/alerts/contracts";
import type {
  OperationsActionRequest,
  OperationsAssignRequest,
  OperationsBulkActionRequest,
  OperationsExceptionListItemResponse,
  OperationsExceptionQueueResponse,
  OperationsResolveRequest,
  OperationsSavedViewFilterRequest,
  OperationsSavedViewResponse,
  OperationsSnoozeRequest,
} from "../features/operations/contracts";
import {
  connectOperationsStream,
  type OperationsConnectionState,
} from "../features/operations/live";

const session = useSessionStore();
const status = ref<"idle" | "loading" | "success" | "error">("idle");
const pageError = ref("");
const actionMessage = ref("");
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
const savedViews = ref<OperationsSavedViewResponse[]>([]);
const assignees = ref<AlertAssigneeResponse[]>([]);
const assignmentSelections = reactive<Record<string, string>>({});
const filters = reactive<OperationsSavedViewFilterRequest>({
  search: "",
  sourceType: "",
  severity: "",
  workflowStatus: "",
  assignedToUserId: "",
  includeSnoozed: false,
});
const selectedIds = ref(new Set<string>());
const focusedId = ref("");
const bulkAction = ref("");
const connectionState = ref<OperationsConnectionState>("idle");
let hubConnection: { stop(): Promise<void> } | null = null;

const focusedItem = computed(
  () => queue.value.items.find((item) => item.id === focusedId.value) ?? null,
);
const selectedCount = computed(() => selectedIds.value.size);
const connectionLabel = computed(() => {
  switch (connectionState.value) {
    case "live":
      return "Live";
    case "reconnecting":
      return "Reconnecting";
    case "offline":
      return "Offline";
    default:
      return "Connecting";
  }
});
const connectionBadgeClass = computed(() =>
  connectionState.value === "live"
    ? "badge text-bg-success"
    : connectionState.value === "reconnecting"
      ? "badge text-bg-warning"
      : "badge text-bg-secondary",
);

function severityBadgeClass(severity: string) {
  return severity === "Critical"
    ? "badge text-bg-danger"
    : severity === "Warning"
      ? "badge text-bg-warning"
      : "badge text-bg-secondary";
}

function workflowBadgeClass(statusValue: string) {
  return statusValue === "Acknowledged"
    ? "badge text-bg-primary"
    : statusValue === "Snoozed"
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
  if (minutes < 60) return `${minutes} min ago`;
  const hours = Math.round(minutes / 60);
  if (hours < 24) return `${hours} h ago`;
  return `${Math.round(hours / 24)} d ago`;
}

function toggleSelection(id: string) {
  const next = new Set(selectedIds.value);
  if (next.has(id)) {
    next.delete(id);
  } else {
    next.add(id);
  }
  selectedIds.value = next;
}

function buildQuery() {
  const params = new URLSearchParams();
  if (filters.search) params.set("search", filters.search);
  if (filters.sourceType) params.set("sourceType", filters.sourceType);
  if (filters.severity) params.set("severity", filters.severity);
  if (filters.workflowStatus)
    params.set("workflowStatus", filters.workflowStatus);
  if (filters.assignedToUserId)
    params.set("assignedToUserId", filters.assignedToUserId);
  if (filters.includeSnoozed) params.set("includeSnoozed", "true");
  const query = params.toString();
  return query.length > 0 ? `?${query}` : "";
}

async function refreshQueue() {
  if (!session.accessToken) return;
  status.value = "loading";
  pageError.value = "";
  try {
    queue.value = await apiRequest<OperationsExceptionQueueResponse>(
      `/api/v1/operations/exceptions${buildQuery()}`,
      {
        token: session.accessToken,
      },
    );
    for (const item of queue.value.items) {
      assignmentSelections[item.id] =
        assignmentSelections[item.id] ?? item.assignedToUserId ?? "";
    }
    if (!focusedId.value && queue.value.items.length > 0) {
      focusedId.value = queue.value.items[0].id;
    }
    status.value = "success";
  } catch {
    status.value = "error";
    pageError.value = "Unable to load the operations center.";
  }
}

async function refreshSavedViews() {
  if (!session.accessToken) return;
  savedViews.value = await apiRequest<OperationsSavedViewResponse[]>(
    "/api/v1/operations/saved-views",
    {
      token: session.accessToken,
    },
  );
}

async function refreshAssignees() {
  if (!session.accessToken) return;
  assignees.value = await apiRequest<AlertAssigneeResponse[]>(
    "/api/v1/alerts/assignees",
    {
      token: session.accessToken,
    },
  );
}

async function refreshAll() {
  await Promise.all([refreshQueue(), refreshSavedViews(), refreshAssignees()]);
}

function applySavedView(view: OperationsSavedViewResponse) {
  filters.search = view.filters.search ?? "";
  filters.sourceType = view.filters.sourceType ?? "";
  filters.severity = view.filters.severity ?? "";
  filters.workflowStatus = view.filters.workflowStatus ?? "";
  filters.assignedToUserId = view.filters.assignedToUserId ?? "";
  filters.includeSnoozed = view.filters.includeSnoozed;
  refreshQueue();
}

async function saveCurrentView() {
  const name = window.prompt("Saved view name", "Morning exceptions");
  if (!name || !session.accessToken) return;
  const shared = window.confirm(
    "Make this saved view available to the whole team?",
  );
  await apiRequest<OperationsSavedViewResponse>(
    "/api/v1/operations/saved-views",
    {
      method: "POST",
      token: session.accessToken,
      body: {
        name,
        isShared: shared,
        filters,
      },
    },
  );
  actionMessage.value = "Saved view created.";
  await refreshSavedViews();
}

async function assign(item: OperationsExceptionListItemResponse) {
  if (!session.accessToken || !assignmentSelections[item.id]) return;
  const request: OperationsAssignRequest = {
    assignedToUserId: assignmentSelections[item.id],
    concurrencyToken: item.concurrencyToken,
  };
  await apiRequest(`/api/v1/operations/exceptions/${item.id}/assign`, {
    method: "POST",
    token: session.accessToken,
    body: request,
  });
  actionMessage.value = "Exception assigned.";
  await refreshQueue();
}

async function acknowledge(item: OperationsExceptionListItemResponse) {
  if (!session.accessToken) return;
  const request: OperationsActionRequest = {
    concurrencyToken: item.concurrencyToken,
  };
  await apiRequest(`/api/v1/operations/exceptions/${item.id}/acknowledge`, {
    method: "POST",
    token: session.accessToken,
    body: request,
  });
  actionMessage.value = "Exception acknowledged.";
  await refreshQueue();
}

async function resolve(item: OperationsExceptionListItemResponse) {
  if (!session.accessToken) return;
  const reason = window.prompt(
    "Resolution reason",
    "Resolved by operator review",
  );
  if (!reason) return;
  const request: OperationsResolveRequest = {
    concurrencyToken: item.concurrencyToken,
    reason,
  };
  await apiRequest(`/api/v1/operations/exceptions/${item.id}/resolve`, {
    method: "POST",
    token: session.accessToken,
    body: request,
  });
  actionMessage.value = "Exception resolved.";
  await refreshQueue();
}

async function snooze(item: OperationsExceptionListItemResponse) {
  if (!session.accessToken) return;
  const reason = window.prompt(
    "Snooze reason",
    "Waiting for field confirmation",
  );
  if (!reason) return;
  const until = new Date(Date.now() + 2 * 60 * 60 * 1000).toISOString();
  const request: OperationsSnoozeRequest = {
    concurrencyToken: item.concurrencyToken,
    snoozedUntilUtc: until,
    reason,
  };
  await apiRequest(`/api/v1/operations/exceptions/${item.id}/snooze`, {
    method: "POST",
    token: session.accessToken,
    body: request,
  });
  actionMessage.value = "Exception snoozed.";
  await refreshQueue();
}

async function runBulkAction() {
  if (!session.accessToken || !bulkAction.value) return;
  const targets = queue.value.items.filter((item) =>
    selectedIds.value.has(item.id),
  );
  const request: OperationsBulkActionRequest = {
    action: bulkAction.value,
    reason:
      bulkAction.value === "resolve"
        ? "Bulk resolution after operator review"
        : bulkAction.value === "snooze"
          ? "Bulk defer until shift follow-up"
          : null,
    snoozedUntilUtc:
      bulkAction.value === "snooze"
        ? new Date(Date.now() + 2 * 60 * 60 * 1000).toISOString()
        : null,
    assignedToUserId: null,
    items: targets.map((item) => ({
      id: item.id,
      concurrencyToken: item.concurrencyToken,
    })),
  };
  await apiRequest("/api/v1/operations/exceptions/bulk", {
    method: "POST",
    token: session.accessToken,
    body: request,
  });
  selectedIds.value = new Set();
  actionMessage.value = "Bulk action completed.";
  await refreshQueue();
}

onMounted(async () => {
  await refreshAll();
  hubConnection = await connectOperationsStream(refreshQueue, (state) => {
    connectionState.value = state;
  });
});

onBeforeUnmount(async () => {
  if (hubConnection) {
    await hubConnection.stop();
  }
});
</script>

<style scoped>
.operations-hero {
  background:
    radial-gradient(
      circle at top right,
      rgba(13, 107, 93, 0.16),
      transparent 34%
    ),
    linear-gradient(180deg, rgba(255, 255, 255, 0.98), #fdfefe);
}

.operations-live-rail {
  display: grid;
  gap: 0.5rem;
  justify-items: end;
}

.operations-toolbar {
  display: grid;
  grid-template-columns:
    minmax(240px, 1.6fr) repeat(4, minmax(140px, 1fr))
    auto;
  gap: 0.75rem;
  align-items: center;
}

.operations-toggle {
  display: flex;
  align-items: center;
  gap: 0.55rem;
  padding: 0 0.3rem;
}

.operations-queue {
  display: grid;
  gap: 0.85rem;
}

.operations-card {
  display: grid;
  gap: 0.9rem;
  padding: 1rem;
  border: 1px solid rgba(15, 23, 42, 0.08);
  border-radius: 1rem;
  background:
    linear-gradient(180deg, rgba(247, 252, 251, 0.94), #ffffff), white;
}

.operations-card-active {
  border-color: rgba(13, 107, 93, 0.35);
  box-shadow: 0 16px 36px rgba(13, 107, 93, 0.08);
}

.operations-card-main {
  display: grid;
  grid-template-columns: auto minmax(0, 1fr) auto;
  gap: 0.9rem;
  align-items: start;
}

.operations-card-copy {
  display: grid;
  gap: 0.45rem;
}

.operations-card-copy p {
  color: var(--muted);
}

.operations-context {
  display: flex;
  flex-wrap: wrap;
  gap: 0.7rem;
  color: var(--muted);
  font-size: 0.9rem;
}

.operations-card-side {
  display: grid;
  justify-items: end;
  color: var(--muted);
  font-size: 0.84rem;
}

.operations-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 0.55rem;
  align-items: center;
}

.action-select {
  max-width: 240px;
}

.bulk-actions {
  display: flex;
  gap: 0.5rem;
  align-items: center;
}

@media (max-width: 1200px) {
  .operations-toolbar {
    grid-template-columns: 1fr 1fr;
  }
}

@media (max-width: 768px) {
  .operations-toolbar,
  .operations-card-main {
    grid-template-columns: 1fr;
  }

  .operations-card-side {
    justify-items: start;
  }
}
</style>
