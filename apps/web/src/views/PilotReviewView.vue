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
            <select v-model="incident.severity" class="form-select">
              <option>P0</option>
              <option>P1</option>
              <option>P2</option></select
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
              <strong>{{ item.severity }} · {{ item.category }}</strong
              ><br />{{ item.summary
              }}<button
                v-if="item.status === 'Open'"
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
type Metrics = {
  analyticsConsent: boolean;
  activeDrivers: number;
  completedMissions: number;
  completeProofs: number;
  openExceptions: number;
  openIncidents: number;
};
type Incident = {
  id: string;
  severity: string;
  status: string;
  category: string;
  summary: string;
};
const session = useSessionStore();
const metrics = ref<Metrics>({
  analyticsConsent: false,
  activeDrivers: 0,
  completedMissions: 0,
  completeProofs: 0,
  openExceptions: 0,
  openIncidents: 0,
});
const incidents = ref<Incident[]>([]);
const consent = ref(false);
const error = ref("");
const message = ref("");
const incident = reactive({
  severity: "P1",
  category: "",
  summary: "",
  workaround: "",
});
const decision = reactive({ outcome: "GO", segment: "", rationale: "" });
const token = () => session.accessToken;
const metricItems = computed(() => [
  { label: "Active drivers", value: metrics.value.activeDrivers },
  { label: "Completed missions", value: metrics.value.completedMissions },
  { label: "Complete proofs", value: metrics.value.completeProofs },
  { label: "Open exceptions", value: metrics.value.openExceptions },
]);
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
  await apiRequest("/api/v1/pilot/consent", {
    method: "PUT",
    token: token(),
    body: { analyticsConsent: consent.value },
  });
  await load();
  message.value = "Pilot measurement preference saved.";
}
async function recordIncident() {
  await apiRequest("/api/v1/pilot/incidents", {
    method: "POST",
    token: token(),
    body: incident,
  });
  Object.assign(incident, {
    severity: "P1",
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
onMounted(load);
</script>
