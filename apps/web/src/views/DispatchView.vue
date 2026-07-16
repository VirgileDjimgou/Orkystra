<template>
  <div class="stacked-page">
    <section class="page-hero">
      <div>
        <span class="eyebrow">Dispatch</span>
        <h1>Mission control board</h1>
        <p>
          Plan missions, assign drivers and vehicles, track state changes, and
          keep an auditable operational timeline inside one tenant-safe flow.
        </p>
      </div>
      <div class="d-flex flex-column gap-2 align-items-end">
        <span class="badge text-bg-dark">{{
          session.user?.organizationName
        }}</span>
        <span class="badge text-bg-light">
          {{ dispatch.missions.length }} mission(s)
        </span>
      </div>
    </section>

    <div class="row g-4">
      <div class="col-xl-5">
        <section class="surface-panel">
          <div class="panel-heading">
            <div>
              <h2>Mission backlog</h2>
              <p>Draft, active, delayed, and completed operations.</p>
            </div>
            <button
              class="btn btn-outline-secondary"
              :disabled="dispatch.missionsStatus === 'loading'"
              @click="refresh"
            >
              {{
                dispatch.missionsStatus === "loading"
                  ? "Refreshing..."
                  : "Refresh"
              }}
            </button>
          </div>

          <div v-if="dispatch.missionsError" class="alert alert-danger">
            {{ dispatch.missionsError }}
          </div>
          <div v-else-if="isInitialMissionLoading" class="empty-placeholder">
            Loading missions...
          </div>
          <div
            v-else-if="dispatch.missions.length === 0"
            class="empty-placeholder"
          >
            No mission planned yet for this organization.
          </div>
          <div v-else class="user-list">
            <article
              v-for="mission in dispatch.missions"
              :key="mission.id"
              :class="[
                'user-card',
                dispatch.selectedMission?.id === mission.id
                  ? 'selected-row'
                  : '',
              ]"
              role="button"
              tabindex="0"
              @click="selectMission(mission.id)"
            >
              <div>
                <strong>{{ mission.reference }}</strong>
                <div class="text-secondary small">{{ mission.title }}</div>
                <div class="text-secondary small">
                  {{
                    formatRange(
                      mission.scheduledStartUtc,
                      mission.scheduledEndUtc,
                    )
                  }}
                </div>
                <div class="text-secondary small">
                  {{ mission.stopCount }} stop(s)
                  <span v-if="mission.driverName">{{
                    ` · ${mission.driverName}`
                  }}</span>
                  <span v-if="mission.vehicleRegistrationNumber">
                    · {{ mission.vehicleRegistrationNumber }}
                  </span>
                </div>
              </div>
              <div class="user-meta">
                <span :class="statusBadgeClass(mission.status)">
                  {{ mission.status }}
                </span>
                <small v-if="mission.simulatedDelayMinutes > 0">
                  +{{ mission.simulatedDelayMinutes }} min
                </small>
                <small v-if="hasMissionLocation(mission)">
                  Live map link
                </small>
              </div>
            </article>
          </div>
        </section>

        <section class="surface-panel mt-4">
          <div class="panel-heading">
            <div>
              <h2>Create a mission</h2>
              <p>
                Use the sprint baseline flow: draft first, then assignment and
                execution.
              </p>
            </div>
          </div>

          <form class="stack-form" @submit.prevent="submitMission">
            <input
              v-model="form.reference"
              class="form-control"
              placeholder="Mission reference"
              required
              maxlength="48"
            />
            <input
              v-model="form.title"
              class="form-control"
              placeholder="Mission title"
              required
              maxlength="160"
            />
            <div class="row g-3">
              <div class="col-md-6">
                <label class="form-label">Scheduled start</label>
                <input
                  v-model="form.scheduledStartUtc"
                  class="form-control"
                  type="datetime-local"
                  required
                />
              </div>
              <div class="col-md-6">
                <label class="form-label">Scheduled end</label>
                <input
                  v-model="form.scheduledEndUtc"
                  class="form-control"
                  type="datetime-local"
                  required
                />
              </div>
            </div>

            <div class="dispatch-stop-stack">
              <div
                v-for="stop in form.stops"
                :key="stop.sequence"
                class="dispatch-stop-card"
              >
                <div class="dispatch-stop-title">Stop {{ stop.sequence }}</div>
                <input
                  v-model="stop.name"
                  class="form-control"
                  placeholder="Stop name"
                  required
                />
                <input
                  v-model="stop.address"
                  class="form-control"
                  placeholder="Address"
                  required
                />
                <input
                  v-model="stop.plannedArrivalUtc"
                  class="form-control"
                  type="datetime-local"
                  required
                />
              </div>
            </div>

            <div v-if="actionMessage" class="alert alert-success mb-0">
              {{ actionMessage }}
            </div>
            <div v-if="dispatch.actionError" class="alert alert-danger mb-0">
              {{ dispatch.actionError }}
            </div>

            <div class="d-flex gap-2">
              <button
                class="btn btn-outline-secondary"
                type="button"
                @click="addStop"
              >
                Add stop
              </button>
              <button
                class="btn btn-primary"
                type="submit"
                :disabled="isSubmitting"
              >
                {{ isSubmitting ? "Creating..." : "Create mission" }}
              </button>
            </div>
          </form>
        </section>
      </div>

      <div class="col-xl-7">
        <section class="surface-panel">
          <div class="panel-heading">
            <div>
              <h2>Mission detail</h2>
              <p>
                {{
                  dispatch.selectedMission
                    ? `Timeline and controls for ${dispatch.selectedMission.reference}`
                    : "Select a mission to inspect its details."
                }}
              </p>
            </div>
          </div>

          <div v-if="dispatch.detailError" class="alert alert-danger">
            {{ dispatch.detailError }}
          </div>
          <div v-else-if="!dispatch.selectedMission" class="empty-placeholder">
            No mission selected yet.
          </div>
          <div v-else class="dispatch-detail-grid">
            <section class="dispatch-detail-section">
              <div class="dispatch-kpi-row">
                <div class="metric-card">
                  <span class="eyebrow">Status</span>
                  <strong>{{ dispatch.selectedMission.status }}</strong>
                </div>
                <div class="metric-card">
                  <span class="eyebrow">Driver</span>
                  <strong>{{ selectedDriverLabel }}</strong>
                </div>
                <div class="metric-card">
                  <span class="eyebrow">Vehicle</span>
                  <strong>{{ selectedVehicleLabel }}</strong>
                </div>
              </div>

              <div class="dispatch-inline-panel">
                <h3>Assignment</h3>
                <div class="row g-3">
                  <div class="col-md-6">
                    <select v-model="assignment.driverId" class="form-select">
                      <option value="">Select driver</option>
                      <option
                        v-for="driver in activeDrivers"
                        :key="driver.id"
                        :value="driver.id"
                      >
                        {{ driver.fullName }}
                      </option>
                    </select>
                  </div>
                  <div class="col-md-6">
                    <select v-model="assignment.vehicleId" class="form-select">
                      <option value="">Select vehicle</option>
                      <option
                        v-for="vehicle in activeVehicles"
                        :key="vehicle.id"
                        :value="vehicle.id"
                      >
                        {{ vehicle.registrationNumber }}
                      </option>
                    </select>
                  </div>
                </div>
                <div class="d-flex gap-2 flex-wrap">
                  <button
                    class="btn btn-outline-primary btn-sm"
                    @click="assignSelectedMission"
                  >
                    Save assignment
                  </button>
                  <RouterLink
                    v-if="hasSelectedMapLink"
                    class="btn btn-outline-secondary btn-sm"
                    :to="mapLink"
                  >
                    Open in fleet map
                  </RouterLink>
                </div>
              </div>

              <div class="dispatch-inline-panel">
                <h3>Status actions</h3>
                <div class="d-flex flex-wrap gap-2">
                  <button
                    v-for="status in availableTransitions"
                    :key="status"
                    class="btn btn-outline-secondary btn-sm"
                    @click="moveTo(status)"
                  >
                    Move to {{ status }}
                  </button>
                </div>
              </div>

              <div class="dispatch-inline-panel">
                <h3>Delay simulation</h3>
                <div class="d-flex gap-2 flex-wrap align-items-center">
                  <input
                    v-model.number="delayMinutes"
                    class="form-control delay-input"
                    type="number"
                    min="1"
                  />
                  <button
                    class="btn btn-outline-warning btn-sm"
                    @click="simulateDelay"
                  >
                    Simulate delay
                  </button>
                </div>
              </div>

              <div class="dispatch-inline-panel">
                <h3>Stops</h3>
                <div class="dispatch-stop-stack">
                  <article
                    v-for="stop in dispatch.selectedMission.stops"
                    :key="stop.id"
                    class="dispatch-stop-card"
                  >
                    <strong>#{{ stop.sequence }} · {{ stop.name }}</strong>
                    <span>{{ stop.address }}</span>
                    <small>{{ formatDateTime(stop.plannedArrivalUtc) }}</small>
                  </article>
                </div>
              </div>

              <div class="dispatch-inline-panel">
                <h3>Pre-departure inspection</h3>
                <div
                  v-if="!dispatch.selectedMission.latestInspection"
                  class="text-secondary"
                >
                  No inspection recorded yet.
                </div>
                <template v-else>
                  <div class="d-flex flex-wrap gap-2 align-items-center">
                    <span :class="inspectionOutcomeBadgeClass">
                      {{ dispatch.selectedMission.latestInspection.outcome }}
                    </span>
                    <span
                      v-if="
                        dispatch.selectedMission.latestInspection
                          .hasBlockingCriticalDefect
                      "
                      class="badge text-bg-danger"
                    >
                      Critical defect blocks departure
                    </span>
                  </div>
                  <small class="text-secondary">
                    {{
                      formatDateTime(
                        dispatch.selectedMission.latestInspection
                          .completedAtUtc,
                      )
                    }}
                  </small>
                  <div
                    v-if="dispatch.selectedMission.latestInspection.notes"
                    class="text-secondary small"
                  >
                    {{ dispatch.selectedMission.latestInspection.notes }}
                  </div>
                  <div class="dispatch-stop-stack">
                    <article
                      v-for="item in dispatch.selectedMission.latestInspection
                        .items"
                      :key="`${item.code}-${item.sequence}`"
                      class="dispatch-stop-card"
                    >
                      <strong>{{ item.label }}</strong>
                      <span>
                        {{
                          item.isPass
                            ? "Passed"
                            : `Defect: ${item.defectSeverity}`
                        }}
                      </span>
                      <small v-if="item.notes">{{ item.notes }}</small>
                      <a
                        v-if="item.photoReadUrl"
                        class="link-secondary small"
                        :href="item.photoReadUrl"
                        target="_blank"
                        rel="noreferrer"
                      >
                        Open attached photo
                      </a>
                    </article>
                  </div>
                </template>
              </div>
            </section>

            <section class="dispatch-detail-section">
              <h3>Audited timeline</h3>
              <div class="history-stack">
                <article
                  v-for="item in dispatch.selectedMission.timeline"
                  :key="item.id"
                  class="history-card"
                >
                  <strong>{{ item.eventType }}</strong>
                  <span>{{ item.description }}</span>
                  <small>{{ formatDateTime(item.occurredAtUtc) }}</small>
                </article>
              </div>

              <div class="dispatch-inline-panel">
                <h3>Delivery proofs</h3>
                <div
                  v-if="dispatch.selectedMission.deliveryProofs.length === 0"
                  class="text-secondary"
                >
                  No delivery proof recorded yet.
                </div>
                <div v-else class="dispatch-stop-stack">
                  <article
                    v-for="proof in dispatch.selectedMission.deliveryProofs"
                    :key="proof.proofId"
                    class="dispatch-stop-card"
                  >
                    <strong>{{ proof.recipientName }}</strong>
                    <span>
                      Signed by {{ proof.signatureName }} on
                      {{ formatDateTime(proof.deliveredAtUtc) }}
                    </span>
                    <small v-if="proof.notes">{{ proof.notes }}</small>
                    <div class="d-flex flex-column gap-1">
                      <a
                        v-for="photo in proof.photos"
                        :key="photo.mediaAssetId"
                        class="link-secondary small"
                        :href="photo.photoReadUrl"
                        target="_blank"
                        rel="noreferrer"
                      >
                        {{ photo.caption || "Open delivery photo" }}
                      </a>
                    </div>
                  </article>
                </div>
              </div>
            </section>
          </div>
        </section>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from "vue";
import { RouterLink } from "vue-router";
import { useSessionStore } from "../features/auth/store";
import { useFleetStore } from "../features/fleet/store";
import type { MissionStatus } from "../features/dispatch/contracts";
import { useDispatchStore } from "../features/dispatch/store";

const session = useSessionStore();
const fleet = useFleetStore();
const dispatch = useDispatchStore();
const isSubmitting = ref(false);
const actionMessage = ref("");
const delayMinutes = ref(15);
const assignment = reactive({
  driverId: "",
  vehicleId: "",
});

const form = reactive({
  reference: "",
  title: "",
  scheduledStartUtc: toLocalInputValue(new Date(Date.now() + 60 * 60 * 1000)),
  scheduledEndUtc: toLocalInputValue(new Date(Date.now() + 3 * 60 * 60 * 1000)),
  stops: [
    {
      sequence: 1,
      name: "Departure depot",
      address: "1 Dispatch Way",
      plannedArrivalUtc: toLocalInputValue(
        new Date(Date.now() + 90 * 60 * 1000),
      ),
    },
    {
      sequence: 2,
      name: "Customer delivery",
      address: "22 Fleet Street",
      plannedArrivalUtc: toLocalInputValue(
        new Date(Date.now() + 2.5 * 60 * 60 * 1000),
      ),
    },
  ],
});

const activeDrivers = computed(() =>
  fleet.drivers.filter((driver) => driver.isActive),
);
const activeVehicles = computed(() =>
  fleet.vehicles.filter((vehicle) => vehicle.isActive),
);
const isInitialMissionLoading = computed(
  () => dispatch.missionsStatus === "loading" && dispatch.missions.length === 0,
);
const hasSelectedMapLink = computed(
  () =>
    !!dispatch.selectedMission?.vehicleId &&
    !!dispatch.selectedMission?.vehicleRegistrationNumber,
);
const selectedDriverLabel = computed(
  () => dispatch.selectedMission?.driverName ?? "Unassigned",
);
const selectedVehicleLabel = computed(
  () => dispatch.selectedMission?.vehicleRegistrationNumber ?? "Unassigned",
);
const mapLink = computed(() => {
  const mission = dispatch.selectedMission;
  if (!mission?.vehicleId) {
    return "/map";
  }

  return {
    path: "/map",
    query: {
      vehicleId: mission.vehicleId,
      missionRef: mission.reference,
    },
  };
});
const availableTransitions = computed<MissionStatus[]>(() => {
  const status = dispatch.selectedMission?.status;
  switch (status) {
    case "Draft":
      return ["Planned", "Cancelled"];
    case "Planned":
      return ["Assigned", "Cancelled"];
    case "Assigned":
      return ["EnRoute", "Delayed", "Cancelled"];
    case "EnRoute":
      return ["Arrived", "Delayed"];
    case "Arrived":
      return ["Completed", "Delayed"];
    case "Delayed":
      return ["Assigned", "EnRoute", "Cancelled"];
    default:
      return [];
  }
});
const inspectionOutcomeBadgeClass = computed(() =>
  dispatch.selectedMission?.latestInspection?.outcome === "Passed"
    ? "badge text-bg-success"
    : "badge text-bg-danger",
);

function toLocalInputValue(value: Date): string {
  return new Date(value.getTime() - value.getTimezoneOffset() * 60000)
    .toISOString()
    .slice(0, 16);
}

function toUtcValue(value: string): string {
  return new Date(value).toISOString();
}

function formatDateTime(value: string): string {
  return new Date(value).toLocaleString();
}

function formatRange(start: string, end: string): string {
  return `${formatDateTime(start)} - ${formatDateTime(end)}`;
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

function hasMissionLocation(
  mission: (typeof dispatch.missions)[number],
): boolean {
  return (
    mission.currentLatitude !== null &&
    mission.currentLongitude !== null &&
    !!mission.vehicleId
  );
}

function addStop() {
  form.stops.push({
    sequence: form.stops.length + 1,
    name: "",
    address: "",
    plannedArrivalUtc: form.scheduledEndUtc,
  });
}

async function refresh() {
  if (!session.accessToken) return;
  await Promise.all([
    dispatch.loadMissions(session.accessToken),
    fleet.loadDrivers(session.accessToken),
    fleet.loadVehicles(session.accessToken),
  ]);
  if (!dispatch.selectedMission && dispatch.missions.length > 0) {
    await selectMission(dispatch.missions[0].id);
  }
}

async function selectMission(missionId: string) {
  if (!session.accessToken) return;
  actionMessage.value = "";
  await dispatch.loadMission(session.accessToken, missionId);
  assignment.driverId = dispatch.selectedMission?.driverId ?? "";
  assignment.vehicleId = dispatch.selectedMission?.vehicleId ?? "";
}

async function submitMission() {
  if (!session.accessToken) return;
  isSubmitting.value = true;
  actionMessage.value = "";
  const created = await dispatch.createMission(session.accessToken, {
    reference: form.reference.trim(),
    title: form.title.trim(),
    scheduledStartUtc: toUtcValue(form.scheduledStartUtc),
    scheduledEndUtc: toUtcValue(form.scheduledEndUtc),
    stops: form.stops.map((stop, index) => ({
      sequence: index + 1,
      name: stop.name.trim(),
      address: stop.address.trim(),
      plannedArrivalUtc: toUtcValue(stop.plannedArrivalUtc),
    })),
  });
  isSubmitting.value = false;

  if (!created) {
    return;
  }

  actionMessage.value = `Mission ${created.reference} created in Draft.`;
  form.reference = "";
  form.title = "";
  assignment.driverId = created.driverId ?? "";
  assignment.vehicleId = created.vehicleId ?? "";
}

async function assignSelectedMission() {
  if (!session.accessToken || !dispatch.selectedMission) return;
  const updated = await dispatch.setAssignment(
    session.accessToken,
    dispatch.selectedMission.id,
    {
      driverId: assignment.driverId,
      vehicleId: assignment.vehicleId,
      rowVersion: dispatch.selectedMission.rowVersion,
    },
  );
  if (updated) {
    actionMessage.value = `Mission ${updated.reference} assignment updated.`;
  }
}

async function moveTo(status: MissionStatus) {
  if (!session.accessToken || !dispatch.selectedMission) return;
  const updated = await dispatch.transitionStatus(
    session.accessToken,
    dispatch.selectedMission.id,
    status,
    dispatch.selectedMission.rowVersion,
  );
  if (updated) {
    actionMessage.value = `Mission ${updated.reference} moved to ${updated.status}.`;
  }
}

async function simulateDelay() {
  if (!session.accessToken || !dispatch.selectedMission) return;
  const updated = await dispatch.simulateDelay(
    session.accessToken,
    dispatch.selectedMission.id,
    {
      delayMinutes: delayMinutes.value,
      rowVersion: dispatch.selectedMission.rowVersion,
    },
  );
  if (updated) {
    actionMessage.value = `Mission ${updated.reference} delayed by ${updated.simulatedDelayMinutes} minutes.`;
  }
}

onMounted(refresh);
</script>

<style scoped>
.dispatch-detail-grid {
  display: grid;
  grid-template-columns: minmax(0, 1.1fr) minmax(280px, 0.9fr);
  gap: 1rem;
}

.dispatch-detail-section {
  display: grid;
  gap: 1rem;
}

.dispatch-kpi-row {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 1rem;
}

.dispatch-inline-panel {
  display: grid;
  gap: 0.85rem;
  padding: 1rem;
  border: 1px solid rgba(15, 23, 42, 0.08);
  border-radius: 1rem;
  background: linear-gradient(180deg, rgba(248, 249, 250, 0.96), #ffffff);
}

.dispatch-inline-panel h3 {
  margin: 0;
  font-size: 1rem;
}

.dispatch-stop-stack {
  display: grid;
  gap: 0.75rem;
}

.dispatch-stop-card {
  display: grid;
  gap: 0.25rem;
  padding: 0.9rem 1rem;
  border-radius: 1rem;
  border: 1px solid rgba(15, 23, 42, 0.08);
  background: #fbfcfd;
}

.dispatch-stop-title {
  font-size: 0.8rem;
  color: var(--muted);
  text-transform: uppercase;
  letter-spacing: 0.06em;
}

.delay-input {
  max-width: 120px;
}

@media (max-width: 1200px) {
  .dispatch-detail-grid,
  .dispatch-kpi-row {
    grid-template-columns: 1fr;
  }
}
</style>
