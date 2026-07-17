<!-- eslint-disable vue/html-closing-bracket-newline, vue/html-indent, vue/multiline-html-element-content-newline -->
<template>
  <div class="stacked-page">
    <section class="page-hero">
      <div>
        <span class="eyebrow">Measured alpha pilot</span>
        <h1>Pilot review</h1>
        <p>
          Aggregate operational evidence only. Enable measurement after
          informing your organization; do not record personal driver analytics
          here.
        </p>
      </div>
      <button class="btn btn-outline-secondary" type="button" @click="load">
        Refresh
      </button>
      <button
        class="btn btn-outline-secondary"
        type="button"
        @click="exportEvidence"
      >
        Export evidence
      </button>
    </section>
    <div v-if="error" class="alert alert-danger" role="alert">{{ error }}</div>
    <div v-if="message" class="alert alert-success" role="status">
      {{ message }}
    </div>
    <section class="surface-panel">
      <h2>Consent and aggregate metrics</h2>
      <p class="text-secondary">
        Metrics are available only after explicit administrator consent.
      </p>
      <div class="form-check form-switch mb-3">
        <input
          id="pilot-consent"
          v-model="consent"
          class="form-check-input"
          type="checkbox"
          @change="saveConsent"
        /><label class="form-check-label" for="pilot-consent"
          >I have informed the organization and opt in to aggregate pilot
          measurement.</label
        >
      </div>
      <button
        class="btn btn-outline-primary mb-3"
        type="button"
        :disabled="!consent"
        @click="collectMetrics"
      >
        Record daily aggregate
      </button>
      <p v-if="metrics.latestDailyMetric" class="text-secondary small">
        Last aggregate refreshed {{ metrics.latestDailyMetric.refreshedAtUtc }}.
        Activity counters cover the current UTC day; mission and proof counters
        are current totals.
      </p>
      <div class="summary-grid">
        <article
          v-for="item in metricItems"
          :key="item.label"
          class="summary-card"
        >
          <strong>{{ item.value }}</strong
          ><small>{{ item.label }}</small>
        </article>
      </div>
    </section>
    <div class="row g-4">
      <section class="col-lg-6">
        <div class="surface-panel">
          <h2>Support incident</h2>
          <form class="stack-form" @submit.prevent="recordIncident">
            <select
              v-model="incident.severity"
              aria-label="Incident severity"
              class="form-select"
            >
              <option :value="0">P0</option>
              <option :value="1">P1</option>
              <option :value="2">P2</option></select
            ><input
              v-model="incident.category"
              class="form-control"
              placeholder="Category"
              required
            /><textarea
              v-model="incident.summary"
              class="form-control"
              placeholder="Summary without personal data"
              required
            ></textarea
            ><input
              v-model="incident.workaround"
              class="form-control"
              placeholder="Workaround (optional)"
            /><button class="btn btn-primary" type="submit">
              Record incident
            </button>
          </form>
          <ul class="list-group list-group-flush mt-3">
            <li
              v-for="item in incidents"
              :key="item.id"
              class="list-group-item"
            >
              <strong
                >{{ severityLabel(item.severity) }} ·
                {{ item.category }}</strong
              ><br />{{ item.summary
              }}<button
                v-if="item.status === 0"
                class="btn btn-sm btn-outline-secondary mt-2"
                type="button"
                @click="resolve(item.id)"
              >
                Resolve
              </button>
            </li>
          </ul>
        </div>
      </section>
      <section class="col-lg-6">
        <div class="surface-panel">
          <h2>Niche decision</h2>
          <form class="stack-form" @submit.prevent="recordDecision">
            <select v-model="decision.outcome" class="form-select">
              <option>GO</option>
              <option>SIMPLIFY</option>
              <option>PIVOT</option>
              <option>STOP</option></select
            ><input
              v-model="decision.segment"
              class="form-control"
              placeholder="Primary segment"
              required
            /><textarea
              v-model="decision.rationale"
              class="form-control"
              placeholder="Evidence-based rationale"
              required
            ></textarea
            ><button class="btn btn-primary" type="submit">
              Record decision
            </button>
          </form>
        </div>
      </section>
    </div>
  </div>
</template>
<script setup lang="ts">
import { computed, onMounted, reactive, ref } from "vue";
import { useSessionStore } from "../features/auth/store";
import { apiRequest } from "../services/api";
type DailyMetric = {
  capturedOnUtc: string;
  activationEvents: number;
  activeDrivers: number;
  returningDrivers: number;
  processedSyncCommands: number;
  completedMissions: number;
  completeProofs: number;
  openExceptions: number;
  refreshedAtUtc: string;
};
type Metrics = {
  analyticsConsent: boolean;
  latestDailyMetric: DailyMetric | null;
  openIncidents: number;
};
type PilotEvidence = {
  analyticsConsent: boolean;
  dailyMetrics: DailyMetric[];
  incidents: Incident[];
  decisions: unknown[];
};
type Incident = {
  id: string;
  severity: number;
  status: number;
  category: string;
  summary: string;
};
const session = useSessionStore();
const metrics = ref<Metrics>({
  analyticsConsent: false,
  latestDailyMetric: null,
  openIncidents: 0,
});
const incidents = ref<Incident[]>([]);
const consent = ref(false);
const error = ref("");
const message = ref("");
const incident = reactive({
  severity: 1,
  category: "",
  summary: "",
  workaround: "",
});
const decision = reactive({ outcome: "GO", segment: "", rationale: "" });
const token = () => session.accessToken;
const metricItems = computed(() => [
  {
    label: "Activation events",
    value: metrics.value.latestDailyMetric?.activationEvents ?? 0,
  },
  {
    label: "Active drivers",
    value: metrics.value.latestDailyMetric?.activeDrivers ?? 0,
  },
  {
    label: "Returning drivers",
    value: metrics.value.latestDailyMetric?.returningDrivers ?? 0,
  },
  {
    label: "Sync commands",
    value: metrics.value.latestDailyMetric?.processedSyncCommands ?? 0,
  },
  {
    label: "Completed missions",
    value: metrics.value.latestDailyMetric?.completedMissions ?? 0,
  },
  {
    label: "Complete proofs",
    value: metrics.value.latestDailyMetric?.completeProofs ?? 0,
  },
  {
    label: "Open exceptions",
    value: metrics.value.latestDailyMetric?.openExceptions ?? 0,
  },
  { label: "Open support incidents", value: metrics.value.openIncidents },
]);
const severityLabel = (severity: number) =>
  ["P0", "P1", "P2"][severity] ?? "Unknown";
async function load() {
  try {
    [metrics.value, incidents.value] = await Promise.all([
      apiRequest<Metrics>("/api/v1/pilot/metrics", { token: token() }),
      apiRequest<Incident[]>("/api/v1/pilot/incidents", { token: token() }),
    ]);
    consent.value = metrics.value.analyticsConsent;
  } catch {
    error.value = "Unable to load pilot evidence.";
  }
}
async function saveConsent() {
  try {
    await apiRequest("/api/v1/pilot/consent", {
      method: "PUT",
      token: token(),
      body: { analyticsConsent: consent.value },
    });
    if (consent.value) await collectMetrics();
    await load();
    message.value = "Pilot measurement preference saved.";
  } catch {
    error.value = "Unable to save the pilot measurement preference.";
  }
}
async function collectMetrics() {
  try {
    await apiRequest<DailyMetric>("/api/v1/pilot/metrics/collect", {
      method: "POST",
      token: token(),
    });
    await load();
    message.value = "Daily aggregate recorded.";
  } catch {
    error.value = "Unable to record the daily aggregate.";
  }
}
async function recordIncident() {
  await apiRequest("/api/v1/pilot/incidents", {
    method: "POST",
    token: token(),
    body: incident,
  });
  Object.assign(incident, {
    severity: 1,
    category: "",
    summary: "",
    workaround: "",
  });
  await load();
}
async function resolve(id: string) {
  await apiRequest(`/api/v1/pilot/incidents/${id}/resolve`, {
    method: "POST",
    token: token(),
    body: {},
  });
  await load();
}
async function recordDecision() {
  await apiRequest("/api/v1/pilot/decisions", {
    method: "POST",
    token: token(),
    body: {
      outcome: decision.outcome,
      primarySegment: decision.segment,
      rationale: decision.rationale,
    },
  });
  message.value = "Pilot decision recorded.";
}
async function exportEvidence() {
  try {
    const evidence = await apiRequest<PilotEvidence>("/api/v1/pilot/export", {
      token: token(),
    });
    const objectUrl = URL.createObjectURL(
      new Blob([JSON.stringify(evidence, null, 2)], {
        type: "application/json",
      }),
    );
    const anchor = document.createElement("a");
    anchor.href = objectUrl;
    anchor.download = "fleetops-pilot-evidence.json";
    anchor.click();
    URL.revokeObjectURL(objectUrl);
    message.value = "Pilot evidence export generated.";
  } catch {
    error.value = "Unable to export pilot evidence.";
  }
}
onMounted(load);
</script>
