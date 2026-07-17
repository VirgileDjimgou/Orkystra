<template>
  <div class="stacked-page">
    <section class="page-hero">
      <div>
        <span class="eyebrow">Guided setup</span>
        <h1>Reach your first test mission</h1>
        <p>
          Import fleet data, invite the team, pair Android and verify readiness
          without database access.
        </p>
      </div>
      <div class="d-flex flex-wrap gap-2">
        <button
          class="btn btn-outline-secondary"
          type="button"
          @click="showHelp"
        >
          Quick tour
        </button>
        <button
          class="btn btn-outline-secondary"
          type="button"
          @click="recordAbandonment"
        >
          Pause setup
        </button>
        <button
          class="btn btn-outline-secondary"
          type="button"
          @click="downloadDiagnostics"
        >
          Export diagnostics
        </button>
        <button class="btn btn-outline-secondary" type="button" @click="load">
          Refresh
        </button>
      </div>
    </section>

    <div v-if="error" class="alert alert-danger" role="alert">{{ error }}</div>
    <div v-if="message" class="alert alert-success" role="status">
      {{ message }}
    </div>

    <section class="surface-panel" aria-labelledby="readiness-heading">
      <div class="panel-heading">
        <div>
          <h2 id="readiness-heading">Activation checklist</h2>
          <p>Progress is derived only from the signed-in organization.</p>
        </div>
        <span
          class="badge text-bg-dark"
          v-text="`${completedSteps}/${checklist.length} ready`"
        ></span>
      </div>
      <div class="summary-grid">
        <article
          v-for="item in checklist"
          :key="item.label"
          class="summary-card"
        >
          <span :class="item.ready ? 'text-success' : 'text-secondary'">
            {{ item.ready ? "Ready" : "To do" }}
          </span>
          <strong>{{ item.value }}</strong>
          <small>{{ item.label }}</small>
        </article>
      </div>
      <p
        v-if="metrics?.minutesToFirstValue != null"
        class="retention-note mb-0"
      >
        First value reached in
        {{ metrics.minutesToFirstValue.toFixed(1) }} minutes.
      </p>
    </section>

    <section class="surface-panel" aria-labelledby="import-heading">
      <div class="panel-heading">
        <div>
          <h2 id="import-heading">CSV import assistant</h2>
          <p>
            Preview validates every line. Fleet records are written only after
            explicit confirmation.
          </p>
        </div>
        <button
          class="btn btn-outline-secondary"
          type="button"
          @click="downloadTemplate"
        >
          Download template
        </button>
      </div>
      <form class="stack-form" @submit.prevent="previewImport">
        <label class="form-label" for="importTarget">Data type</label>
        <select
          id="importTarget"
          v-model="importForm.targetType"
          class="form-select"
        >
          <option value="vehicles">Vehicles</option>
          <option value="drivers">Drivers</option>
          <option value="devices">Devices</option>
        </select>
        <label class="form-label" for="importCsv">CSV content</label>
        <textarea
          id="importCsv"
          v-model="importForm.csv"
          class="form-control font-monospace"
          rows="8"
          required
          spellcheck="false"
        ></textarea>
        <button class="btn btn-primary" type="submit" :disabled="busy">
          {{ busy ? "Validating..." : "Preview import" }}
        </button>
      </form>

      <div v-if="preview" class="mt-4" aria-live="polite">
        <h3 class="h5">Preview result</h3>
        <p>
          {{ preview.rowCount }} data row(s),
          {{ preview.errors.length }} error(s). Preview expires
          {{ formatDate(preview.expiresAtUtc) }}.
        </p>
        <div v-if="preview.errors.length" class="table-responsive">
          <table class="table table-sm align-middle">
            <thead>
              <tr>
                <th>Line</th>
                <th>Field</th>
                <th>Correction required</th>
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="rowError in preview.errors"
                :key="`${rowError.line}-${rowError.field}-${rowError.message}`"
              >
                <td>{{ rowError.line || "File" }}</td>
                <td>{{ rowError.field }}</td>
                <td>{{ rowError.message }}</td>
              </tr>
            </tbody>
          </table>
        </div>
        <button
          class="btn btn-success"
          type="button"
          :disabled="busy || !preview.canConfirm"
          @click="confirmImport"
        >
          Confirm validated import
        </button>
      </div>
    </section>

    <div class="row g-4">
      <div class="col-xl-6">
        <section
          class="surface-panel h-100"
          aria-labelledby="invitation-heading"
        >
          <h2 id="invitation-heading">Invite an operator or driver</h2>
          <p>
            The one-time token expires in seven days. Driver invitations link an
            imported driver profile before Android pairing.
          </p>
          <form class="stack-form" @submit.prevent="invite">
            <label class="form-label" for="inviteName">Full name</label>
            <input
              id="inviteName"
              v-model="invitation.fullName"
              class="form-control"
              required
            />
            <label class="form-label" for="inviteEmail">Email</label>
            <input
              id="inviteEmail"
              v-model="invitation.email"
              class="form-control"
              type="email"
              required
            />
            <label class="form-label" for="inviteRole">Role</label>
            <select
              id="inviteRole"
              v-model="invitation.role"
              class="form-select"
            >
              <option value="Operator">Operator</option>
              <option value="Driver">Driver</option>
            </select>
            <template v-if="invitation.role === 'Driver'">
              <label
                class="form-label"
                for="inviteDriver"
                v-text="'Driver profile'"
              ></label>
              <select
                id="inviteDriver"
                v-model="invitation.driverId"
                class="form-select"
                required
              >
                <option value="">Select an unlinked driver</option>
                <option
                  v-for="driver in availableDrivers"
                  :key="driver.id"
                  :value="driver.id"
                >
                  {{ driver.fullName }} — {{ driver.licenseNumber }}
                </option>
              </select>
            </template>
            <button class="btn btn-primary" type="submit" :disabled="busy">
              Create invitation
            </button>
          </form>
          <div v-if="invitationToken" class="secret-banner" role="status">
            <strong>One-time invitation token</strong>
            <p>Share through an approved private channel.</p>
            <code>{{ invitationToken }}</code>
          </div>
        </section>
      </div>

      <div class="col-xl-6">
        <section class="surface-panel h-100" aria-labelledby="pairing-heading">
          <h2 id="pairing-heading">Pair a driver device</h2>
          <p>
            Generate a six-digit, single-use code. It expires after ten minutes
            and only works for the selected tenant-linked driver.
          </p>
          <label
            class="form-label"
            for="driverUser"
            v-text="'Activated driver account'"
          ></label>
          <select id="driverUser" v-model="driverUserId" class="form-select">
            <option value="">Select a driver account</option>
            <option
              v-for="user in driverUsers"
              :key="user.userId"
              :value="user.userId"
            >
              {{ user.fullName }}
            </option>
          </select>
          <button
            class="btn btn-primary mt-3"
            type="button"
            :disabled="busy || !driverUserId"
            @click="pair"
          >
            Create pairing code
          </button>
          <div v-if="pairingCode" class="secret-banner" role="status">
            <strong>Pairing code</strong>
            <p>Enter this code on the Android sign-in screen.</p>
            <code>{{ pairingCode }}</code>
          </div>
        </section>
      </div>
    </div>

    <section class="surface-panel" aria-labelledby="sample-heading">
      <div class="panel-heading">
        <div>
          <h2 id="sample-heading">Optional test workspace</h2>
          <p>
            Create an isolated vehicle, driver, tracker and assigned mission.
            Remove all four explicitly before switching to real operations.
          </p>
        </div>
        <button
          v-if="!status?.hasSampleData"
          class="btn btn-outline-primary"
          type="button"
          :disabled="busy"
          @click="createSampleData"
        >
          Add test data
        </button>
        <button
          v-else
          class="btn btn-outline-danger"
          type="button"
          :disabled="busy"
          @click="removeSampleData"
        >
          Remove test data
        </button>
      </div>
      <div
        class="alert mb-0"
        :class="status?.hasSampleData ? 'alert-warning' : 'alert-light'"
      >
        {{
          status?.hasSampleData
            ? "Test data is active in this organization."
            : "No onboarding test data is active."
        }}
      </div>
    </section>

    <section
      v-if="tourVisible"
      class="surface-panel"
      aria-labelledby="tour-heading"
      tabindex="-1"
    >
      <h2 id="tour-heading">Five-minute setup tour</h2>
      <ol>
        <li>Enable administrator MFA in Security &amp; data.</li>
        <li>
          Download a template, preview each CSV and correct every line error.
        </li>
        <li>Invite an operator and a driver linked to an imported profile.</li>
        <li>Generate the Android pairing code and verify the first sync.</li>
        <li>
          Create, assign and complete a test mission; then remove optional test
          data.
        </li>
      </ol>
      <p class="mb-0">
        If setup stalls, export diagnostics above. The file contains technical
        counts and identifiers, never names, email addresses, phone numbers or
        tokens.
      </p>
    </section>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from "vue";
import { apiRequest } from "../services/api";
import { useSessionStore } from "../features/auth/store";

type Status = {
  vehicles: number;
  drivers: number;
  devices: number;
  operators: number;
  driverAccounts: number;
  pairedDriverSessions: number;
  activeDeviceAssignments: number;
  complianceDocuments: number;
  missions: number;
  completedMissions: number;
  adminMfaEnabled: boolean;
  hasSampleData: boolean;
  startedAtUtc: string | null;
  firstValueAtUtc: string | null;
};
type Metrics = { minutesToFirstValue: number | null };
type User = {
  userId: string;
  fullName: string;
  role: string;
  driverId: string | null;
};
type Driver = { id: string; fullName: string; licenseNumber: string };
type ImportError = { line: number; field: string; message: string };
type ImportPreview = {
  previewId: string;
  targetType: string;
  rowCount: number;
  errors: ImportError[];
  expiresAtUtc: string;
  canConfirm: boolean;
  rowVersion: number;
};

const session = useSessionStore();
const status = ref<Status | null>(null);
const metrics = ref<Metrics | null>(null);
const users = ref<User[]>([]);
const fleetDrivers = ref<Driver[]>([]);
const preview = ref<ImportPreview | null>(null);
const error = ref("");
const message = ref("");
const busy = ref(false);
const invitationToken = ref("");
const pairingCode = ref("");
const driverUserId = ref("");
const tourVisible = ref(false);
const invitation = reactive({
  fullName: "",
  email: "",
  role: "Operator",
  driverId: "",
});
const importForm = reactive({
  targetType: "vehicles",
  csv: "registrationNumber,displayName\nFLEET-001,Service Van 1\n",
});

const driverUsers = computed(() =>
  users.value.filter((user) => user.role === "Driver"),
);
const linkedDriverIds = computed(
  () =>
    new Set(
      driverUsers.value.flatMap((user) =>
        user.driverId ? [user.driverId] : [],
      ),
    ),
);
const availableDrivers = computed(() =>
  fleetDrivers.value.filter((driver) => !linkedDriverIds.value.has(driver.id)),
);
const checklist = computed(() => [
  {
    label: "Administrator MFA",
    value: status.value?.adminMfaEnabled ? "Enabled" : "Required",
    ready: !!status.value?.adminMfaEnabled,
  },
  {
    label: "Operator accounts",
    value: status.value?.operators ?? 0,
    ready: (status.value?.operators ?? 0) > 0,
  },
  {
    label: "Driver profiles",
    value: status.value?.drivers ?? 0,
    ready: (status.value?.drivers ?? 0) > 0,
  },
  {
    label: "Driver accounts",
    value: `${status.value?.driverAccounts ?? 0} / ${status.value?.pairedDriverSessions ?? 0} paired`,
    ready: (status.value?.pairedDriverSessions ?? 0) > 0,
  },
  {
    label: "Vehicles",
    value: status.value?.vehicles ?? 0,
    ready: (status.value?.vehicles ?? 0) > 0,
  },
  {
    label: "Devices",
    value: `${status.value?.devices ?? 0} / ${status.value?.activeDeviceAssignments ?? 0} assigned`,
    ready: (status.value?.activeDeviceAssignments ?? 0) > 0,
  },
  {
    label: "Documents",
    value: status.value?.complianceDocuments ?? 0,
    ready: (status.value?.complianceDocuments ?? 0) > 0,
  },
  {
    label: "Test missions",
    value: status.value?.missions ?? 0,
    ready: (status.value?.missions ?? 0) > 0,
  },
  {
    label: "First value",
    value: status.value?.completedMissions ?? 0,
    ready: (status.value?.completedMissions ?? 0) > 0,
  },
]);
const completedSteps = computed(
  () => checklist.value.filter((item) => item.ready).length,
);
const options = () => ({ token: session.accessToken! });

async function load() {
  if (!session.accessToken) return;
  error.value = "";
  try {
    const [
      loadedStatus,
      loadedMetrics,
      loadedUsers,
      loadedDrivers,
      latestPreview,
    ] = await Promise.all([
      apiRequest<Status>("/api/v1/onboarding/status", options()),
      apiRequest<Metrics>("/api/v1/onboarding/metrics", options()),
      apiRequest<User[]>("/api/v1/admin/users", options()),
      apiRequest<Driver[]>("/api/v1/fleet/drivers", options()),
      apiRequest<ImportPreview | undefined>(
        "/api/v1/onboarding/imports/latest",
        options(),
      ),
    ]);
    status.value = loadedStatus;
    metrics.value = loadedMetrics;
    users.value = loadedUsers;
    fleetDrivers.value = loadedDrivers;
    preview.value = latestPreview ?? null;
    if (latestPreview) importForm.targetType = latestPreview.targetType;
  } catch {
    error.value = "Unable to load guided setup.";
  }
}

async function previewImport() {
  await run(async () => {
    preview.value = await apiRequest<ImportPreview>(
      "/api/v1/onboarding/imports/preview",
      {
        ...options(),
        method: "POST",
        body: importForm,
      },
    );
    message.value = preview.value.canConfirm
      ? "Preview is valid and ready for confirmation."
      : "Correct the listed line errors, then preview the file again.";
  });
}

async function confirmImport() {
  if (!preview.value) return;
  await run(async () => {
    const result = await apiRequest<{
      created: number;
      updated: number;
      skipped: number;
    }>(`/api/v1/onboarding/imports/${preview.value!.previewId}/confirm`, {
      ...options(),
      method: "POST",
      body: { rowVersion: preview.value!.rowVersion },
    });
    message.value = `Import confirmed: ${result.created} created, ${result.updated} updated, ${result.skipped} unchanged.`;
    preview.value = null;
    await load();
  });
}

async function invite() {
  await run(async () => {
    const body = {
      ...invitation,
      driverId: invitation.role === "Driver" ? invitation.driverId : null,
    };
    const result = await apiRequest<{ token: string }>(
      "/api/v1/onboarding/invitations",
      {
        ...options(),
        method: "POST",
        body,
      },
    );
    invitationToken.value = result.token;
    message.value = "Invitation created. It remains valid for seven days.";
  });
}

async function pair() {
  await run(async () => {
    const result = await apiRequest<{ code: string }>(
      "/api/v1/onboarding/pairing-codes",
      {
        ...options(),
        method: "POST",
        body: { userId: driverUserId.value },
      },
    );
    pairingCode.value = result.code;
    message.value = "Pairing code created. It expires in ten minutes.";
  });
}

async function createSampleData() {
  await run(async () => {
    await apiRequest("/api/v1/onboarding/sample-data", {
      ...options(),
      method: "POST",
    });
    message.value = "Isolated test data created.";
    await load();
  });
}

async function removeSampleData() {
  if (
    !window.confirm(
      "Remove the onboarding vehicle, driver, tracker and mission?",
    )
  )
    return;
  await run(async () => {
    await apiRequest("/api/v1/onboarding/sample-data", {
      ...options(),
      method: "DELETE",
    });
    message.value = "Onboarding test data removed.";
    await load();
  });
}

async function showHelp() {
  tourVisible.value = true;
  await apiRequest("/api/v1/onboarding/events", {
    ...options(),
    method: "POST",
    body: { eventName: "help_opened", step: "tour" },
  });
}

async function recordAbandonment() {
  await apiRequest("/api/v1/onboarding/events", {
    ...options(),
    method: "POST",
    body: { eventName: "onboarding_abandoned", step: "guided_setup" },
  });
  message.value =
    "Progress is saved. Return to Guided setup to resume the latest preview.";
}

async function downloadTemplate() {
  await downloadText(
    `/api/v1/onboarding/imports/template/${importForm.targetType}`,
    `fleetops-${importForm.targetType}-template.csv`,
    "text/csv",
  );
}

async function downloadDiagnostics() {
  await downloadText(
    "/api/v1/onboarding/diagnostics",
    "fleetops-onboarding-diagnostics.json",
    "application/json",
  );
}

async function downloadText(path: string, fileName: string, type: string) {
  await run(async () => {
    const content = await apiRequest<string>(path, {
      ...options(),
      responseType: "text",
    });
    const url = URL.createObjectURL(new Blob([content], { type }));
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = fileName;
    anchor.click();
    URL.revokeObjectURL(url);
  });
}

async function run(action: () => Promise<void>) {
  busy.value = true;
  error.value = "";
  message.value = "";
  try {
    await action();
  } catch {
    error.value =
      "The onboarding action could not be completed. Review the data and try again.";
  } finally {
    busy.value = false;
  }
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat(undefined, {
    dateStyle: "medium",
    timeStyle: "short",
  }).format(new Date(value));
}

onMounted(load);
</script>
