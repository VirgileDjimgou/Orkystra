<template>
  <div class="stacked-page">
    <section class="page-hero">
      <div>
        <span class="eyebrow">Sprint 07</span>
        <h1>Alert center</h1>
        <p>
          Scan deterministic rules, assign ownership, acknowledge incidents, and
          configure compliance or maintenance inputs without leaving the tenant
          workspace.
        </p>
      </div>
      <div class="d-flex flex-column gap-2 align-items-end">
        <span class="badge text-bg-dark">{{
          session.user?.organizationName
        }}</span>
        <span class="badge text-bg-light">{{ alerts.length }} alert(s)</span>
      </div>
    </section>

    <div class="row g-3">
      <div class="col-sm-6 col-xl-3">
        <div class="metric-card">
          <span class="text-secondary">Open alerts</span>
          <strong class="display-6">{{ summary?.openCount ?? 0 }}</strong>
          <small>Unresolved operational items</small>
        </div>
      </div>
      <div class="col-sm-6 col-xl-3">
        <div class="metric-card">
          <span class="text-secondary">Critical alerts</span>
          <strong class="display-6">{{ summary?.criticalCount ?? 0 }}</strong>
          <small>Immediate action required</small>
        </div>
      </div>
      <div class="col-sm-6 col-xl-3">
        <div class="metric-card">
          <span class="text-secondary">Maintenance due</span>
          <strong class="display-6">{{
            summary?.maintenanceCount ?? 0
          }}</strong>
          <small>Date and mileage plans</small>
        </div>
      </div>
      <div class="col-sm-6 col-xl-3">
        <div class="metric-card">
          <span class="text-secondary">Compliance due</span>
          <strong class="display-6">{{ summary?.complianceCount ?? 0 }}</strong>
          <small>Driver and vehicle documents</small>
        </div>
      </div>
    </div>

    <section class="surface-panel mt-4">
      <div class="panel-heading">
        <div>
          <h2>Run deterministic scan</h2>
          <p>Use the same engine as the worker to refresh alerts on demand.</p>
        </div>
        <div class="d-flex gap-2">
          <button
            class="btn btn-outline-secondary"
            :disabled="status === 'loading'"
            @click="refreshAll"
          >
            Refresh
          </button>
          <button
            class="btn btn-primary"
            :disabled="isScanning"
            @click="runScan"
          >
            {{ isScanning ? "Scanning..." : "Run scan" }}
          </button>
        </div>
      </div>
      <div v-if="scanSummary" class="alert alert-success mb-0">
        Scan complete: {{ scanSummary.createdAlerts }} created,
        {{ scanSummary.refreshedAlerts }} refreshed,
        {{ scanSummary.resolvedAlerts }} resolved,
        {{ scanSummary.emailFailures }} email failure(s).
      </div>
      <div v-if="pageError" class="alert alert-danger mb-0 mt-3">
        {{ pageError }}
      </div>
      <div v-if="actionMessage" class="alert alert-success mb-0 mt-3">
        {{ actionMessage }}
      </div>
    </section>

    <div class="row g-4 mt-1">
      <div class="col-xl-7">
        <section class="surface-panel">
          <div class="panel-heading">
            <div>
              <h2>Operational alerts</h2>
              <p>
                Assignment and acknowledgment remain role-aware and
                server-validated.
              </p>
            </div>
          </div>

          <div v-if="status === 'loading'" class="empty-placeholder">
            Loading alerts...
          </div>
          <div v-else-if="alerts.length === 0" class="empty-placeholder">
            No alerts available yet. Add documents or maintenance plans, then
            run a scan.
          </div>
          <div v-else class="alerts-stack">
            <article v-for="alert in alerts" :key="alert.id" class="alert-card">
              <div class="alert-card-header">
                <div>
                  <div class="d-flex gap-2 align-items-center flex-wrap">
                    <strong>{{ alert.title }}</strong>
                    <span :class="severityBadgeClass(alert.severity)">{{
                      alert.severity
                    }}</span>
                    <span :class="statusBadgeClass(alert.status)">{{
                      alert.status
                    }}</span>
                  </div>
                  <div class="text-secondary small">
                    {{ alert.targetLabel }} · {{ alert.message }}
                  </div>
                </div>
                <small>{{ formatDateTime(alert.lastDetectedAtUtc) }}</small>
              </div>

              <div class="alert-meta-row">
                <span>Rule: {{ alert.ruleType }}</span>
                <span v-if="alert.assignedToDisplayName">
                  Assigned to {{ alert.assignedToDisplayName }}
                </span>
                <span v-if="alert.acknowledgedByDisplayName">
                  Acknowledged by {{ alert.acknowledgedByDisplayName }}
                </span>
              </div>

              <div v-if="alert.resolvedAtUtc" class="text-secondary small">
                Resolved on {{ formatDateTime(alert.resolvedAtUtc) }}
              </div>

              <div v-else class="d-flex flex-wrap gap-2 align-items-center">
                <select
                  v-model="assignmentSelections[alert.id]"
                  class="form-select form-select-sm assignment-select"
                  :disabled="assignees.length === 0 || busyAlertId === alert.id"
                >
                  <option value="">Assign owner</option>
                  <option
                    v-for="assignee in assignees"
                    :key="assignee.userId"
                    :value="assignee.userId"
                  >
                    {{ assignee.fullName }} · {{ assignee.role }}
                  </option>
                </select>
                <button
                  class="btn btn-outline-primary btn-sm"
                  :disabled="
                    !assignmentSelections[alert.id] || busyAlertId === alert.id
                  "
                  @click="assignAlert(alert.id, alert.rowVersion)"
                >
                  Assign
                </button>
                <button
                  class="btn btn-outline-success btn-sm"
                  :disabled="
                    alert.status === 'Acknowledged' || busyAlertId === alert.id
                  "
                  @click="acknowledgeAlert(alert.id, alert.rowVersion)"
                >
                  Acknowledge
                </button>
              </div>
            </article>
          </div>
        </section>
      </div>

      <div class="col-xl-5">
        <section class="surface-panel">
          <div class="panel-heading">
            <div>
              <h2>Recent notifications</h2>
              <p>Trace of in-app and development-email delivery attempts.</p>
            </div>
          </div>
          <div v-if="notifications.length === 0" class="empty-placeholder">
            No notifications sent yet.
          </div>
          <div v-else class="history-stack">
            <article
              v-for="notification in notifications"
              :key="notification.id"
              class="history-card"
            >
              <strong>{{ notification.subject }}</strong>
              <span>{{ notification.channel }}</span>
              <small>{{ formatDateTime(notification.sentAtUtc) }}</small>
            </article>
          </div>
        </section>

        <section v-if="session.isAdmin" class="surface-panel mt-4">
          <div class="panel-heading">
            <div>
              <h2>Compliance setup</h2>
              <p>
                Admins can register upcoming expirations for vehicles and
                drivers.
              </p>
            </div>
          </div>

          <form class="stack-form" @submit.prevent="createVehicleDocument">
            <select
              v-model="vehicleDocumentForm.vehicleId"
              class="form-select"
              required
            >
              <option value="">Select vehicle</option>
              <option
                v-for="vehicle in fleet.vehicles"
                :key="vehicle.id"
                :value="vehicle.id"
              >
                {{ vehicle.registrationNumber }}
              </option>
            </select>
            <input
              v-model="vehicleDocumentForm.documentType"
              class="form-control"
              placeholder="Document type"
              required
            />
            <input
              v-model="vehicleDocumentForm.documentNumber"
              class="form-control"
              placeholder="Document number"
              required
            />
            <input
              v-model="vehicleDocumentForm.expiresAtUtc"
              class="form-control"
              type="datetime-local"
              required
            />
            <textarea
              v-model="vehicleDocumentForm.notes"
              class="form-control"
              rows="2"
              placeholder="Notes (optional)"
            />
            <button
              class="btn btn-outline-primary"
              type="submit"
              :disabled="busyForm === 'vehicle-document'"
            >
              {{
                busyForm === "vehicle-document"
                  ? "Saving..."
                  : "Add vehicle document"
              }}
            </button>
          </form>

          <form class="stack-form mt-4" @submit.prevent="createDriverDocument">
            <select
              v-model="driverDocumentForm.driverId"
              class="form-select"
              required
            >
              <option value="">Select driver</option>
              <option
                v-for="driver in fleet.drivers"
                :key="driver.id"
                :value="driver.id"
              >
                {{ driver.fullName }}
              </option>
            </select>
            <input
              v-model="driverDocumentForm.documentType"
              class="form-control"
              placeholder="Document type"
              required
            />
            <input
              v-model="driverDocumentForm.documentNumber"
              class="form-control"
              placeholder="Document number"
              required
            />
            <input
              v-model="driverDocumentForm.expiresAtUtc"
              class="form-control"
              type="datetime-local"
              required
            />
            <textarea
              v-model="driverDocumentForm.notes"
              class="form-control"
              rows="2"
              placeholder="Notes (optional)"
            />
            <button
              class="btn btn-outline-primary"
              type="submit"
              :disabled="busyForm === 'driver-document'"
            >
              {{
                busyForm === "driver-document"
                  ? "Saving..."
                  : "Add driver document"
              }}
            </button>
          </form>
        </section>

        <section v-if="session.isAdmin" class="surface-panel mt-4">
          <div class="panel-heading">
            <div>
              <h2>Maintenance setup</h2>
              <p>
                Define mileage/date plans and update odometer readings for
                deterministic scans.
              </p>
            </div>
          </div>

          <form class="stack-form" @submit.prevent="createMaintenancePlan">
            <select
              v-model="maintenanceForm.vehicleId"
              class="form-select"
              required
            >
              <option value="">Select vehicle</option>
              <option
                v-for="vehicle in fleet.vehicles"
                :key="vehicle.id"
                :value="vehicle.id"
              >
                {{ vehicle.registrationNumber }}
              </option>
            </select>
            <input
              v-model="maintenanceForm.title"
              class="form-control"
              placeholder="Plan title"
              required
            />
            <div class="row g-3">
              <div class="col-md-6">
                <input
                  v-model.number="maintenanceForm.intervalKilometers"
                  class="form-control"
                  type="number"
                  min="0"
                  placeholder="Interval km"
                />
              </div>
              <div class="col-md-6">
                <input
                  v-model.number="maintenanceForm.intervalDays"
                  class="form-control"
                  type="number"
                  min="0"
                  placeholder="Interval days"
                />
              </div>
            </div>
            <div class="row g-3">
              <div class="col-md-6">
                <input
                  v-model.number="maintenanceForm.lastCompletedOdometerKm"
                  class="form-control"
                  type="number"
                  min="0"
                  placeholder="Last completion km"
                  required
                />
              </div>
              <div class="col-md-6">
                <input
                  v-model="maintenanceForm.lastCompletedAtUtc"
                  class="form-control"
                  type="datetime-local"
                  required
                />
              </div>
            </div>
            <button
              class="btn btn-outline-primary"
              type="submit"
              :disabled="busyForm === 'maintenance-plan'"
            >
              {{
                busyForm === "maintenance-plan"
                  ? "Saving..."
                  : "Create maintenance plan"
              }}
            </button>
          </form>

          <form class="stack-form mt-4" @submit.prevent="updateVehicleOdometer">
            <select
              v-model="odometerForm.vehicleId"
              class="form-select"
              required
            >
              <option value="">Select vehicle</option>
              <option
                v-for="vehicle in fleet.vehicles"
                :key="vehicle.id"
                :value="vehicle.id"
              >
                {{ vehicle.registrationNumber }} ·
                {{ vehicle.currentOdometerKm }} km
              </option>
            </select>
            <input
              v-model.number="odometerForm.currentOdometerKm"
              class="form-control"
              type="number"
              min="0"
              placeholder="Current odometer km"
              required
            />
            <button
              class="btn btn-outline-secondary"
              type="submit"
              :disabled="busyForm === 'odometer'"
            >
              {{ busyForm === "odometer" ? "Saving..." : "Update odometer" }}
            </button>
          </form>
        </section>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from "vue";
import { useSessionStore } from "../features/auth/store";
import { useFleetStore } from "../features/fleet/store";
import { apiRequest } from "../services/api";
import type {
  AlertAssigneeResponse,
  AlertListItemResponse,
  AlertNotificationResponse,
  AlertSeverity,
  AlertStatus,
  AlertSummaryResponse,
  CreateComplianceDocumentRequest,
  CreateVehicleMaintenancePlanRequest,
  ScanAlertsResponse,
} from "../features/alerts/contracts";
import {
  normalizeAlertListItemResponse,
  normalizeAlertNotificationResponse,
  normalizeAlertSummaryResponse,
} from "../features/alerts/contracts";

const session = useSessionStore();
const fleet = useFleetStore();
const summary = ref<AlertSummaryResponse | null>(null);
const alerts = ref<AlertListItemResponse[]>([]);
const notifications = ref<AlertNotificationResponse[]>([]);
const assignees = ref<AlertAssigneeResponse[]>([]);
const assignmentSelections = reactive<Record<string, string>>({});
const status = ref<"idle" | "loading" | "success" | "error">("idle");
const pageError = ref("");
const actionMessage = ref("");
const scanSummary = ref<ScanAlertsResponse | null>(null);
const isScanning = ref(false);
const busyAlertId = ref<string | null>(null);
const busyForm = ref("");

const vehicleDocumentForm = reactive({
  vehicleId: "",
  documentType: "Insurance",
  documentNumber: "",
  expiresAtUtc: toLocalInputValue(
    new Date(Date.now() + 10 * 24 * 60 * 60 * 1000),
  ),
  notes: "",
});

const driverDocumentForm = reactive({
  driverId: "",
  documentType: "License",
  documentNumber: "",
  expiresAtUtc: toLocalInputValue(
    new Date(Date.now() + 7 * 24 * 60 * 60 * 1000),
  ),
  notes: "",
});

const maintenanceForm = reactive({
  vehicleId: "",
  title: "Oil change",
  intervalKilometers: 5000 as number | null,
  intervalDays: 90 as number | null,
  lastCompletedOdometerKm: 0,
  lastCompletedAtUtc: toLocalInputValue(new Date()),
});

const odometerForm = reactive({
  vehicleId: "",
  currentOdometerKm: 0,
});

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

function severityBadgeClass(severity: AlertSeverity): string {
  switch (severity) {
    case "Critical":
      return "badge text-bg-danger";
    case "Warning":
      return "badge text-bg-warning";
    default:
      return "badge text-bg-secondary";
  }
}

function statusBadgeClass(statusValue: AlertStatus): string {
  switch (statusValue) {
    case "Acknowledged":
      return "badge text-bg-primary";
    case "Resolved":
      return "badge text-bg-success";
    default:
      return "badge text-bg-secondary";
  }
}

async function refreshAll() {
  if (!session.accessToken) {
    return;
  }

  status.value = "loading";
  pageError.value = "";
  try {
    const [dashboard, alertItems, notificationItems, assigneeItems] =
      await Promise.all([
        apiRequest<AlertSummaryResponse>("/api/v1/alerts/dashboard", {
          token: session.accessToken,
        }),
        apiRequest<AlertListItemResponse[]>("/api/v1/alerts", {
          token: session.accessToken,
        }),
        apiRequest<AlertNotificationResponse[]>(
          "/api/v1/alerts/notifications",
          {
            token: session.accessToken,
          },
        ),
        apiRequest<AlertAssigneeResponse[]>("/api/v1/alerts/assignees", {
          token: session.accessToken,
        }),
        fleet.loadVehicles(session.accessToken),
        fleet.loadDrivers(session.accessToken),
      ]);

    summary.value = normalizeAlertSummaryResponse(dashboard);
    alerts.value = alertItems.map(normalizeAlertListItemResponse);
    notifications.value = notificationItems.map(
      normalizeAlertNotificationResponse,
    );
    assignees.value = assigneeItems;
    for (const alert of alertItems) {
      assignmentSelections[alert.id] =
        alert.assignedToUserId ?? assignmentSelections[alert.id] ?? "";
    }
    status.value = "success";
  } catch {
    status.value = "error";
    pageError.value = "Unable to load alert center data.";
  }
}

async function runScan() {
  if (!session.accessToken) return;
  isScanning.value = true;
  pageError.value = "";
  actionMessage.value = "";
  try {
    scanSummary.value = await apiRequest<ScanAlertsResponse>(
      "/api/v1/alerts/scan",
      {
        method: "POST",
        token: session.accessToken,
      },
    );
    await refreshAll();
  } catch {
    pageError.value = "Unable to run the alert scan.";
  } finally {
    isScanning.value = false;
  }
}

async function assignAlert(alertId: string, rowVersion: number) {
  if (!session.accessToken || !assignmentSelections[alertId]) return;
  busyAlertId.value = alertId;
  pageError.value = "";
  actionMessage.value = "";
  try {
    await apiRequest(`/api/v1/alerts/${alertId}/assign`, {
      method: "POST",
      token: session.accessToken,
      body: {
        assignedToUserId: assignmentSelections[alertId],
        rowVersion,
      },
    });
    actionMessage.value = "Alert assignment updated.";
    await refreshAll();
  } catch {
    pageError.value = "Unable to assign the selected alert.";
  } finally {
    busyAlertId.value = null;
  }
}

async function acknowledgeAlert(alertId: string, rowVersion: number) {
  if (!session.accessToken) return;
  busyAlertId.value = alertId;
  pageError.value = "";
  actionMessage.value = "";
  try {
    await apiRequest(`/api/v1/alerts/${alertId}/acknowledge`, {
      method: "POST",
      token: session.accessToken,
      body: { rowVersion },
    });
    actionMessage.value = "Alert acknowledged.";
    await refreshAll();
  } catch {
    pageError.value = "Unable to acknowledge the selected alert.";
  } finally {
    busyAlertId.value = null;
  }
}

async function createVehicleDocument() {
  if (!session.accessToken) return;
  busyForm.value = "vehicle-document";
  pageError.value = "";
  actionMessage.value = "";
  try {
    const body: CreateComplianceDocumentRequest = {
      documentType: vehicleDocumentForm.documentType.trim(),
      documentNumber: vehicleDocumentForm.documentNumber.trim(),
      expiresAtUtc: toUtcValue(vehicleDocumentForm.expiresAtUtc),
      notes: vehicleDocumentForm.notes.trim() || null,
    };
    await apiRequest(
      `/api/v1/fleet/vehicles/${vehicleDocumentForm.vehicleId}/documents`,
      {
        method: "POST",
        token: session.accessToken,
        body,
      },
    );
    actionMessage.value = "Vehicle document registered.";
    vehicleDocumentForm.documentNumber = "";
    await refreshAll();
  } catch {
    pageError.value = "Unable to save the vehicle document.";
  } finally {
    busyForm.value = "";
  }
}

async function createDriverDocument() {
  if (!session.accessToken) return;
  busyForm.value = "driver-document";
  pageError.value = "";
  actionMessage.value = "";
  try {
    const body: CreateComplianceDocumentRequest = {
      documentType: driverDocumentForm.documentType.trim(),
      documentNumber: driverDocumentForm.documentNumber.trim(),
      expiresAtUtc: toUtcValue(driverDocumentForm.expiresAtUtc),
      notes: driverDocumentForm.notes.trim() || null,
    };
    await apiRequest(
      `/api/v1/fleet/drivers/${driverDocumentForm.driverId}/documents`,
      {
        method: "POST",
        token: session.accessToken,
        body,
      },
    );
    actionMessage.value = "Driver document registered.";
    driverDocumentForm.documentNumber = "";
    await refreshAll();
  } catch {
    pageError.value = "Unable to save the driver document.";
  } finally {
    busyForm.value = "";
  }
}

async function createMaintenancePlan() {
  if (!session.accessToken) return;
  busyForm.value = "maintenance-plan";
  pageError.value = "";
  actionMessage.value = "";
  try {
    const body: CreateVehicleMaintenancePlanRequest = {
      title: maintenanceForm.title.trim(),
      intervalKilometers:
        maintenanceForm.intervalKilometers &&
        maintenanceForm.intervalKilometers > 0
          ? maintenanceForm.intervalKilometers
          : null,
      intervalDays:
        maintenanceForm.intervalDays && maintenanceForm.intervalDays > 0
          ? maintenanceForm.intervalDays
          : null,
      lastCompletedOdometerKm: maintenanceForm.lastCompletedOdometerKm,
      lastCompletedAtUtc: toUtcValue(maintenanceForm.lastCompletedAtUtc),
    };
    await apiRequest(
      `/api/v1/fleet/vehicles/${maintenanceForm.vehicleId}/maintenance-plans`,
      {
        method: "POST",
        token: session.accessToken,
        body,
      },
    );
    actionMessage.value = "Maintenance plan created.";
    await refreshAll();
  } catch {
    pageError.value = "Unable to create the maintenance plan.";
  } finally {
    busyForm.value = "";
  }
}

async function updateVehicleOdometer() {
  if (!session.accessToken) return;
  const vehicle = fleet.vehicles.find(
    (item) => item.id === odometerForm.vehicleId,
  );
  if (!vehicle) {
    pageError.value = "Select a vehicle before updating the odometer.";
    return;
  }

  busyForm.value = "odometer";
  pageError.value = "";
  actionMessage.value = "";
  try {
    await apiRequest(
      `/api/v1/fleet/vehicles/${odometerForm.vehicleId}/odometer`,
      {
        method: "POST",
        token: session.accessToken,
        body: {
          currentOdometerKm: odometerForm.currentOdometerKm,
          rowVersion: vehicle.rowVersion,
        },
      },
    );
    actionMessage.value = "Vehicle odometer updated.";
    await refreshAll();
  } catch {
    pageError.value = "Unable to update the vehicle odometer.";
  } finally {
    busyForm.value = "";
  }
}

onMounted(refreshAll);
</script>

<style scoped>
.alerts-stack {
  display: grid;
  gap: 0.9rem;
}

.alert-card {
  display: grid;
  gap: 0.8rem;
  padding: 1rem;
  border-radius: 1rem;
  border: 1px solid rgba(15, 23, 42, 0.08);
  background: linear-gradient(180deg, rgba(248, 249, 250, 0.96), #ffffff);
}

.alert-card-header {
  display: flex;
  justify-content: space-between;
  gap: 1rem;
}

.alert-meta-row {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
  color: var(--muted);
  font-size: 0.9rem;
}

.assignment-select {
  max-width: 260px;
}

@media (max-width: 768px) {
  .alert-card-header {
    flex-direction: column;
  }
}
</style>
