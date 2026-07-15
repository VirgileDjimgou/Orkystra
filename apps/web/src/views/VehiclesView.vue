<template>
  <div class="stacked-page">
    <section class="page-hero">
      <div>
        <span class="eyebrow">Fleet registry</span>
        <h1>Vehicles</h1>
        <p>
          Manage the vehicles of your current organization. Registration numbers
          are unique per tenant and statuses can be toggled by administrators.
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
              <h2>Registered vehicles</h2>
              <p>Vehicles are scoped to the signed-in organization.</p>
            </div>
            <button
              class="btn btn-outline-secondary"
              :disabled="fleet.vehiclesStatus === 'loading'"
              @click="refresh"
            >
              {{
                fleet.vehiclesStatus === "loading" ? "Refreshing..." : "Refresh"
              }}
            </button>
          </div>

          <div v-if="fleet.vehiclesError" class="alert alert-danger">
            {{ fleet.vehiclesError }}
          </div>
          <div
            v-else-if="
              fleet.vehiclesStatus === 'loading' && fleet.vehicles.length === 0
            "
            class="empty-placeholder"
          >
            Loading vehicles...
          </div>
          <div
            v-else-if="fleet.vehicles.length === 0"
            class="empty-placeholder"
          >
            No vehicles registered yet.
          </div>
          <div v-else class="user-list">
            <article
              v-for="vehicle in fleet.vehicles"
              :key="vehicle.id"
              class="user-card"
            >
              <div>
                <strong>{{ vehicle.registrationNumber }}</strong>
                <div class="text-secondary small">
                  {{ vehicle.displayName }}
                </div>
              </div>
              <div class="user-meta">
                <span
                  :class="vehicle.isActive ? 'text-success' : 'text-danger'"
                >
                  {{ vehicle.isActive ? "Active" : "Inactive" }}
                </span>
                <template v-if="session.isAdmin">
                  <button
                    v-if="vehicle.isActive"
                    class="btn btn-outline-secondary btn-sm"
                    :disabled="busyId === vehicle.id"
                    @click="toggle(vehicle, false)"
                  >
                    Deactivate
                  </button>
                  <button
                    v-else
                    class="btn btn-outline-success btn-sm"
                    :disabled="busyId === vehicle.id"
                    @click="toggle(vehicle, true)"
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
              <h2>Register a vehicle</h2>
              <p>New vehicles are created inside the current tenant.</p>
            </div>
          </div>

          <form class="stack-form" @submit.prevent="submit">
            <label class="form-label" for="registration">Registration</label>
            <input
              id="registration"
              v-model="form.registrationNumber"
              class="form-control"
              required
              maxlength="32"
            />

            <label class="form-label" for="displayName">Display name</label>
            <input
              id="displayName"
              v-model="form.displayName"
              class="form-control"
              required
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
              {{ isSubmitting ? "Creating..." : "Create vehicle" }}
            </button>
          </form>
        </section>

        <section v-if="session.isAdmin" class="surface-panel mt-4">
          <div class="panel-heading">
            <div>
              <h2>CSV import</h2>
              <p>
                Format: registrationNumber,displayName. Existing vehicles are
                updated.
              </p>
            </div>
          </div>

          <form class="stack-form" @submit.prevent="submitImport">
            <textarea
              v-model="importCsv"
              class="form-control"
              rows="5"
              required
              placeholder="registration,display&#10;NW-300,City service van"
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
              <p>Only administrators can register new vehicles.</p>
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
import type { VehicleResponse } from "../features/fleet/contracts";

const session = useSessionStore();
const fleet = useFleetStore();
const isSubmitting = ref(false);
const formError = ref("");
const formSuccess = ref("");
const busyId = ref<string | null>(null);
const importCsv = ref("registration,display\nNW-300,City service van");
const isImporting = ref(false);

const form = reactive({
  registrationNumber: "",
  displayName: "",
});

async function refresh() {
  if (!session.accessToken) return;
  await fleet.loadVehicles(session.accessToken);
}

async function submit() {
  if (!session.accessToken) return;
  isSubmitting.value = true;
  formError.value = "";
  formSuccess.value = "";
  const created = await fleet.createVehicle(session.accessToken, {
    registrationNumber: form.registrationNumber.trim(),
    displayName: form.displayName.trim(),
  });
  isSubmitting.value = false;
  if (created) {
    formSuccess.value = `Vehicle ${created.registrationNumber} registered.`;
    form.registrationNumber = "";
    form.displayName = "";
  } else {
    formError.value =
      fleet.actionError ||
      "Unable to register the vehicle with the provided data.";
  }
}

async function toggle(vehicle: VehicleResponse, activate: boolean) {
  if (!session.accessToken) return;
  busyId.value = vehicle.id;
  const ok = await fleet.setVehicleStatus(
    session.accessToken,
    vehicle.id,
    activate,
  );
  busyId.value = null;
  if (!ok) {
    formError.value = fleet.actionError;
  }
}

async function submitImport() {
  if (!session.accessToken) return;
  isImporting.value = true;
  formError.value = "";
  formSuccess.value = "";
  const summary = await fleet.importVehicles(
    session.accessToken,
    importCsv.value,
  );
  isImporting.value = false;
  if (summary) {
    formSuccess.value = `CSV import complete: ${summary.created} created, ${summary.updated} updated, ${summary.skipped} skipped.`;
  } else {
    formError.value = fleet.actionError || "Unable to import vehicles.";
  }
}

onMounted(refresh);
</script>
