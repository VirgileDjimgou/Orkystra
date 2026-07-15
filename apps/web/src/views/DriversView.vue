<template>
  <div class="stacked-page">
    <section class="page-hero">
      <div>
        <span class="eyebrow">Fleet registry</span>
        <h1>Drivers</h1>
        <p>
          Manage drivers scoped to the current organization. License numbers are
          unique per tenant and can be toggled by administrators.
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
              <h2>Registered drivers</h2>
              <p>Drivers are scoped to the signed-in organization.</p>
            </div>
            <button
              class="btn btn-outline-secondary"
              :disabled="fleet.driversStatus === 'loading'"
              @click="refresh"
            >
              {{
                fleet.driversStatus === "loading" ? "Refreshing..." : "Refresh"
              }}
            </button>
          </div>

          <div v-if="fleet.driversError" class="alert alert-danger">
            {{ fleet.driversError }}
          </div>
          <div
            v-else-if="
              fleet.driversStatus === 'loading' && fleet.drivers.length === 0
            "
            class="empty-placeholder"
          >
            Loading drivers...
          </div>
          <div v-else-if="fleet.drivers.length === 0" class="empty-placeholder">
            No drivers registered yet.
          </div>
          <div v-else class="user-list">
            <article
              v-for="driver in fleet.drivers"
              :key="driver.id"
              class="user-card"
            >
              <div>
                <strong>{{ driver.fullName }}</strong>
                <div class="text-secondary small">
                  License {{ driver.licenseNumber }}
                  <span v-if="driver.phoneNumber">
                    &middot; {{ driver.phoneNumber }}
                  </span>
                </div>
              </div>
              <div class="user-meta">
                <span :class="driver.isActive ? 'text-success' : 'text-danger'">
                  {{ driver.isActive ? "Active" : "Inactive" }}
                </span>
                <template v-if="session.isAdmin">
                  <button
                    v-if="driver.isActive"
                    class="btn btn-outline-secondary btn-sm"
                    :disabled="busyId === driver.id"
                    @click="toggle(driver, false)"
                  >
                    Deactivate
                  </button>
                  <button
                    v-else
                    class="btn btn-outline-success btn-sm"
                    :disabled="busyId === driver.id"
                    @click="toggle(driver, true)"
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
              <h2>Register a driver</h2>
              <p>New drivers are created inside the current tenant.</p>
            </div>
          </div>

          <form class="stack-form" @submit.prevent="submit">
            <label class="form-label" for="fullName">Full name</label>
            <input
              id="fullName"
              v-model="form.fullName"
              class="form-control"
              required
              maxlength="160"
            />

            <label class="form-label" for="license">License number</label>
            <input
              id="license"
              v-model="form.licenseNumber"
              class="form-control"
              required
              maxlength="64"
            />

            <label class="form-label" for="phone">Phone (optional)</label>
            <input
              id="phone"
              v-model="form.phoneNumber"
              class="form-control"
              maxlength="40"
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
              {{ isSubmitting ? "Creating..." : "Create driver" }}
            </button>
          </form>
        </section>

        <section v-if="session.isAdmin" class="surface-panel mt-4">
          <div class="panel-heading">
            <div>
              <h2>CSV import</h2>
              <p>
                Format: fullName,licenseNumber,phone. Existing drivers are
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
              placeholder="name,license,phone&#10;Taylor Reed,NW-DL-300,+1-555-0300"
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
              <p>Only administrators can register new drivers.</p>
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
import type { DriverResponse } from "../features/fleet/contracts";

const session = useSessionStore();
const fleet = useFleetStore();
const isSubmitting = ref(false);
const formError = ref("");
const formSuccess = ref("");
const busyId = ref<string | null>(null);
const importCsv = ref("name,license,phone\nTaylor Reed,NW-DL-300,+1-555-0300");
const isImporting = ref(false);

const form = reactive({
  fullName: "",
  licenseNumber: "",
  phoneNumber: "",
});

async function refresh() {
  if (!session.accessToken) return;
  await fleet.loadDrivers(session.accessToken);
}

async function submit() {
  if (!session.accessToken) return;
  isSubmitting.value = true;
  formError.value = "";
  formSuccess.value = "";
  const created = await fleet.createDriver(session.accessToken, {
    fullName: form.fullName.trim(),
    licenseNumber: form.licenseNumber.trim(),
    phoneNumber: form.phoneNumber.trim() ? form.phoneNumber.trim() : null,
  });
  isSubmitting.value = false;
  if (created) {
    formSuccess.value = `Driver ${created.fullName} registered.`;
    form.fullName = "";
    form.licenseNumber = "";
    form.phoneNumber = "";
  } else {
    formError.value =
      fleet.actionError ||
      "Unable to register the driver with the provided data.";
  }
}

async function toggle(driver: DriverResponse, activate: boolean) {
  if (!session.accessToken) return;
  busyId.value = driver.id;
  const ok = await fleet.setDriverStatus(
    session.accessToken,
    driver.id,
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
  const summary = await fleet.importDrivers(
    session.accessToken,
    importCsv.value,
  );
  isImporting.value = false;
  if (summary) {
    formSuccess.value = `CSV import complete: ${summary.created} created, ${summary.updated} updated, ${summary.skipped} skipped.`;
  } else {
    formError.value = fleet.actionError || "Unable to import drivers.";
  }
}

onMounted(refresh);
</script>
