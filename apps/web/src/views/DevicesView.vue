<template>
  <div class="stacked-page">
    <section class="page-hero">
      <div>
        <span class="eyebrow">Fleet registry</span>
        <h1>GPS devices</h1>
        <p>
          Manage GPS trackers and their vehicle assignments. An active device
          can hold at most one active assignment at a time.
        </p>
      </div>
      <span class="badge text-bg-dark">{{
        session.user?.organizationName
      }}</span>
    </section>

    <div class="row g-4">
      <div class="col-lg-7">
        <section class="surface-panel">
          <div class="panel-heading">
            <div>
              <h2>Registered devices</h2>
              <p>Devices are scoped to the signed-in organization.</p>
            </div>
            <button
              class="btn btn-outline-secondary"
              :disabled="fleet.devicesStatus === 'loading'"
              @click="refresh"
            >
              {{
                fleet.devicesStatus === "loading" ? "Refreshing..." : "Refresh"
              }}
            </button>
          </div>

          <div v-if="fleet.devicesError" class="alert alert-danger">
            {{ fleet.devicesError }}
          </div>
          <div
            v-else-if="
              fleet.devicesStatus === 'loading' && fleet.devices.length === 0
            "
            class="empty-placeholder"
          >
            Loading devices...
          </div>
          <div v-else-if="fleet.devices.length === 0" class="empty-placeholder">
            No devices registered yet.
          </div>
          <div v-else class="user-list">
            <article
              v-for="device in fleet.devices"
              :key="device.id"
              class="user-card"
            >
              <div>
                <strong>{{ device.serialNumber }}</strong>
                <div class="text-secondary small">
                  {{ device.displayName || "—" }}
                </div>
                <div class="text-secondary small">
                  Assigned to:
                  <span v-if="device.activeAssignment">
                    {{ device.activeAssignment.vehicleRegistrationNumber }}
                    (since
                    {{ formatDate(device.activeAssignment.assignedAtUtc) }})
                  </span>
                  <span v-else>not assigned</span>
                </div>
              </div>
              <div class="user-meta">
                <span :class="device.isActive ? 'text-success' : 'text-danger'">
                  {{ device.isActive ? "Active" : "Inactive" }}
                </span>

                <select
                  v-if="!device.activeAssignment"
                  v-model="assignmentForm[device.id]"
                  class="form-select form-select-sm"
                >
                  <option value="">Select vehicle</option>
                  <option
                    v-for="vehicle in assignableVehicles()"
                    :key="vehicle.id"
                    :value="vehicle.id"
                  >
                    {{ vehicle.registrationNumber }}
                  </option>
                </select>
                <button
                  v-if="!device.activeAssignment"
                  class="btn btn-outline-primary btn-sm"
                  :disabled="!assignmentForm[device.id] || busyId === device.id"
                  @click="assign(device.id)"
                >
                  Assign
                </button>

                <button
                  v-if="device.activeAssignment"
                  class="btn btn-outline-secondary btn-sm"
                  :disabled="busyId === device.id"
                  @click="closeAssignment(device.id)"
                >
                  Close assignment
                </button>

                <template v-if="session.isAdmin">
                  <button
                    v-if="device.isActive"
                    class="btn btn-outline-secondary btn-sm"
                    :disabled="busyId === device.id"
                    @click="toggle(device, false)"
                  >
                    Deactivate
                  </button>
                  <button
                    v-else
                    class="btn btn-outline-success btn-sm"
                    :disabled="busyId === device.id"
                    @click="toggle(device, true)"
                  >
                    Activate
                  </button>
                </template>
              </div>
            </article>
          </div>
        </section>
      </div>

      <div class="col-lg-5">
        <section v-if="session.isAdmin" class="surface-panel">
          <div class="panel-heading">
            <div>
              <h2>Register a device</h2>
              <p>New GPS devices are created inside the current tenant.</p>
            </div>
          </div>

          <form class="stack-form" @submit.prevent="submit">
            <label class="form-label" for="serial">Serial number</label>
            <input
              id="serial"
              v-model="form.serialNumber"
              class="form-control"
              required
              maxlength="64"
            />

            <label class="form-label" for="deviceName">Display name</label>
            <input
              id="deviceName"
              v-model="form.displayName"
              class="form-control"
              maxlength="128"
            />

            <div v-if="formError" class="alert alert-danger mb-0">
              {{ formError }}
            </div>
            <div v-if="formSuccess" class="alert alert-success mb-0">
              {{ formSuccess }}
            </div>

            <button
              class="btn btn-primary"
              type="submit"
              :disabled="isSubmitting"
            >
              {{ isSubmitting ? "Creating..." : "Create device" }}
            </button>
          </form>
        </section>

        <section v-if="session.isAdmin" class="surface-panel mt-4">
          <div class="panel-heading">
            <div>
              <h2>CSV import</h2>
              <p>
                Format: serialNumber,displayName. Existing devices are updated.
              </p>
            </div>
          </div>

          <form class="stack-form" @submit.prevent="submitImport">
            <textarea
              v-model="importCsv"
              class="form-control"
              rows="5"
              required
              placeholder="serial,display&#10;NW-GPS-300,City van tracker"
            />
            <button
              class="btn btn-outline-primary"
              type="submit"
              :disabled="isImporting"
            >
              {{ isImporting ? "Importing..." : "Import CSV" }}
            </button>
          </form>
        </section>

        <section v-else class="surface-panel">
          <div class="panel-heading">
            <div>
              <h2>Read-only access</h2>
              <p>Only administrators can register new devices.</p>
            </div>
          </div>
        </section>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from "vue";
import { useSessionStore } from "../features/auth/store";
import { useFleetStore } from "../features/fleet/store";
import type { GpsDeviceResponse } from "../features/fleet/contracts";

const session = useSessionStore();
const fleet = useFleetStore();
const isSubmitting = ref(false);
const formError = ref("");
const formSuccess = ref("");
const busyId = ref<string | null>(null);
const assignmentForm = reactive<Record<string, string>>({});
const importCsv = ref("serial,display\nNW-GPS-300,City van tracker");
const isImporting = ref(false);

const form = reactive({
  serialNumber: "",
  displayName: "",
});

function assignableVehicles() {
  return fleet.vehicles.filter((vehicle) => vehicle.isActive);
}

function formatDate(value: string): string {
  return new Date(value).toLocaleString();
}

async function refresh() {
  if (!session.accessToken) return;
  await Promise.all([
    fleet.loadDevices(session.accessToken),
    fleet.loadVehicles(session.accessToken),
  ]);
}

async function submit() {
  if (!session.accessToken) return;
  isSubmitting.value = true;
  formError.value = "";
  formSuccess.value = "";
  const created = await fleet.createDevice(session.accessToken, {
    serialNumber: form.serialNumber.trim(),
    displayName: form.displayName.trim() ? form.displayName.trim() : null,
  });
  isSubmitting.value = false;
  if (created) {
    formSuccess.value = `Device ${created.serialNumber} registered.`;
    form.serialNumber = "";
    form.displayName = "";
  } else {
    formError.value =
      fleet.actionError ||
      "Unable to register the device with the provided data.";
  }
}

async function toggle(device: GpsDeviceResponse, activate: boolean) {
  if (!session.accessToken) return;
  busyId.value = device.id;
  const ok = await fleet.setDeviceStatus(
    session.accessToken,
    device.id,
    activate,
  );
  busyId.value = null;
  if (!ok) {
    formError.value = fleet.actionError;
  }
}

async function assign(deviceId: string) {
  if (!session.accessToken) return;
  const vehicleId = assignmentForm[deviceId];
  if (!vehicleId) return;
  busyId.value = deviceId;
  const ok = await fleet.assignDevice(session.accessToken, deviceId, vehicleId);
  busyId.value = null;
  if (ok) {
    assignmentForm[deviceId] = "";
    formError.value = "";
    formSuccess.value = "Device assignment created.";
  } else {
    formError.value = fleet.actionError || "Unable to assign the device.";
  }
}

async function closeAssignment(deviceId: string) {
  if (!session.accessToken) return;
  busyId.value = deviceId;
  const ok = await fleet.closeAssignment(session.accessToken, deviceId);
  busyId.value = null;
  if (!ok) {
    formError.value = fleet.actionError || "Unable to close the assignment.";
  }
}

async function submitImport() {
  if (!session.accessToken) return;
  isImporting.value = true;
  formError.value = "";
  formSuccess.value = "";
  const summary = await fleet.importDevices(
    session.accessToken,
    importCsv.value,
  );
  isImporting.value = false;
  if (summary) {
    formSuccess.value = `CSV import complete: ${summary.created} created, ${summary.updated} updated, ${summary.skipped} skipped.`;
  } else {
    formError.value = fleet.actionError || "Unable to import devices.";
  }
}

onMounted(refresh);
</script>
