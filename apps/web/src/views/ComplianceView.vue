<template>
  <section class="stack" aria-labelledby="compliance-title">
    <header class="page-heading">
      <div>
        <p class="eyebrow">Customer configuration</p>
        <h1 id="compliance-title">Compliance workspace</h1>
        <p>{{ policy?.disclaimer }}</p>
      </div>
      <button class="btn btn-secondary" type="button" @click="exportAudit">
        Export audit CSV
      </button>
    </header>
    <p v-if="error" class="alert alert-error" role="alert">{{ error }}</p>
    <article class="card">
      <h2>Assignment policy</h2>
      <label class="checkbox-row">
        <input
          v-model="blocksAssignments"
          type="checkbox"
          :disabled="!session.isAdmin"
        />
        Block assignments with missing, expired, or unapproved blocking
        documents
      </label>
      <button
        v-if="session.isAdmin"
        class="btn btn-primary"
        type="button"
        @click="savePolicy"
      >
        Save policy
      </button>
    </article>
    <article class="card">
      <h2>Coverage matrix</h2>
      <p v-if="loading">Loading coverage…</p>
      <div v-else class="table-wrap">
        <table>
          <caption class="sr-only">
            Compliance coverage by vehicle and driver
          </caption>
          <thead>
            <tr>
              <th>Subject</th>
              <th>Document</th>
              <th>Status</th>
              <th>Expiry</th>
              <th>Risk</th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="row in matrix"
              :key="`${row.subjectId}-${row.documentType}`"
              :class="{ 'table-risk': row.isRisk }"
            >
              <td>
                {{ row.subjectLabel }} <small>({{ row.subjectType }})</small>
              </td>
              <td>{{ row.documentType }}</td>
              <td>{{ row.status }}</td>
              <td>
                {{
                  row.expiresAtUtc
                    ? new Date(row.expiresAtUtc).toLocaleDateString()
                    : "Missing"
                }}
              </td>
              <td>{{ row.isRisk ? "Needs attention" : "Covered" }}</td>
            </tr>
          </tbody>
        </table>
      </div>
    </article>
    <article class="card">
      <h2>Inspection campaigns</h2>
      <p v-if="campaigns.length === 0">
        No inspection campaigns have been created.
      </p>
      <ul v-else>
        <li v-for="campaign in campaigns" :key="campaign.id">
          <strong>{{ campaign.name }}</strong> — {{ campaign.status }} ·
          {{
            campaign.tasks.filter((task) => task.status === "Submitted").length
          }}/{{ campaign.tasks.length }} submitted
        </li>
      </ul>
    </article>
  </section>
</template>
<script setup lang="ts">
import { onMounted, ref } from "vue";
import { apiRequest } from "../services/api";
import { useSessionStore } from "../features/auth/store";
import type {
  Campaign,
  ComplianceMatrixRow,
  CompliancePolicy,
} from "../features/compliance/contracts";
const session = useSessionStore();
const matrix = ref<ComplianceMatrixRow[]>([]);
const campaigns = ref<Campaign[]>([]);
const policy = ref<CompliancePolicy>();
const blocksAssignments = ref(false);
const loading = ref(true);
const error = ref("");
async function load() {
  loading.value = true;
  error.value = "";
  try {
    const token = session.accessToken;
    [matrix.value, campaigns.value, policy.value] = await Promise.all([
      apiRequest<ComplianceMatrixRow[]>("/api/v1/compliance/matrix", { token }),
      apiRequest<Campaign[]>("/api/v1/compliance/campaigns", { token }),
      apiRequest<CompliancePolicy>("/api/v1/compliance/policy", { token }),
    ]);
    blocksAssignments.value = policy.value.blocksAssignments;
  } catch (cause) {
    error.value =
      cause instanceof Error ? cause.message : "Unable to load compliance.";
  } finally {
    loading.value = false;
  }
}
async function savePolicy() {
  if (!policy.value) return;
  try {
    policy.value = await apiRequest<CompliancePolicy>(
      "/api/v1/compliance/policy",
      {
        method: "PUT",
        token: session.accessToken,
        body: {
          blocksAssignments: blocksAssignments.value,
          rowVersion: policy.value.rowVersion,
        },
      },
    );
  } catch (cause) {
    error.value =
      cause instanceof Error ? cause.message : "Unable to save policy.";
  }
}
async function exportAudit() {
  try {
    const csv = await apiRequest<string>("/api/v1/compliance/audit-export", {
      token: session.accessToken,
      responseType: "text",
    });
    const url = URL.createObjectURL(new Blob([csv], { type: "text/csv" }));
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = "compliance-audit.csv";
    anchor.click();
    URL.revokeObjectURL(url);
  } catch (cause) {
    error.value =
      cause instanceof Error ? cause.message : "Unable to export audit.";
  }
}
onMounted(load);
</script>
