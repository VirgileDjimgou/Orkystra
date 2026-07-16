<template>
  <div class="stacked-page">
    <section class="page-hero">
      <div>
        <span class="eyebrow">Tenant-aware workspace</span>
        <h1>Operations overview</h1>
        <p>
          Signed in as {{ session.user?.fullName }} for
          {{ session.user?.organizationName }}.
        </p>
      </div>
      <div class="d-flex flex-column gap-2 align-items-end">
        <span class="badge text-bg-dark">{{
          session.user?.roles.join(", ")
        }}</span>
        <span v-if="summary" class="badge text-bg-warning">
          {{ summary.openCount }} open alert(s)
        </span>
      </div>
    </section>

    <div v-if="status === 'error'" class="alert alert-danger">
      {{ errorMessage }}
    </div>
    <div v-else-if="status === 'loading'" class="empty-placeholder">
      Loading operational dashboard...
    </div>

    <div class="row g-3">
      <div v-for="card in cards" :key="card.label" class="col-sm-6 col-xl-3">
        <div class="metric-card">
          <span class="text-secondary">{{ card.label }}</span>
          <strong class="display-6">{{ card.value }}</strong>
          <small>{{ card.note }}</small>
        </div>
      </div>
    </div>

    <div class="row g-4 mt-1">
      <div class="col-xl-7">
        <section class="surface-panel">
          <div class="panel-heading">
            <div>
              <h2>Priority alerts</h2>
              <p>Most recent signals requiring operational attention.</p>
            </div>
          </div>

          <div
            v-if="!summary || summary.topAlerts.length === 0"
            class="empty-placeholder"
          >
            No alerts detected yet for this organization.
          </div>
          <div v-else class="user-list">
            <article
              v-for="alert in summary.topAlerts"
              :key="alert.id"
              class="user-card"
            >
              <div>
                <strong>{{ alert.title }}</strong>
                <div class="text-secondary small">
                  {{ alert.targetLabel }} · {{ alert.message }}
                </div>
              </div>
              <div class="user-meta">
                <span :class="severityBadgeClass(alert.severity)">
                  {{ alert.severity }}
                </span>
                <small>{{ formatDateTime(alert.lastDetectedAtUtc) }}</small>
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
              <p>In-app and development email traces emitted by alert scans.</p>
            </div>
          </div>

          <div
            v-if="!summary || summary.recentNotifications.length === 0"
            class="empty-placeholder"
          >
            No alert notifications sent yet.
          </div>
          <div v-else class="history-stack">
            <article
              v-for="notification in summary.recentNotifications"
              :key="notification.id"
              class="history-card"
            >
              <strong>{{ notification.subject }}</strong>
              <span>{{ notification.channel }}</span>
              <small>{{ formatDateTime(notification.sentAtUtc) }}</small>
            </article>
          </div>
        </section>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { useSessionStore } from "../features/auth/store";
import { apiRequest } from "../services/api";
import type {
  AlertSeverity,
  AlertSummaryResponse,
} from "../features/alerts/contracts";
import { normalizeAlertSummaryResponse } from "../features/alerts/contracts";

const session = useSessionStore();
const summary = ref<AlertSummaryResponse | null>(null);
const status = ref<"idle" | "loading" | "success" | "error">("idle");
const errorMessage = ref("");

const cards = computed(() => {
  if (!summary.value) {
    return [
      {
        label: "Organization",
        value: session.user?.organizationName ?? "-",
        note: "Resolved from JWT claims",
      },
      {
        label: "Role set",
        value: session.user?.roles.join(", ") ?? "-",
        note: "Server-authorized navigation",
      },
      {
        label: "User admin",
        value: session.canManageUsers ? "Enabled" : "Restricted",
        note: "Only Admin can manage users",
      },
      {
        label: "Alert center",
        value: "Pending",
        note: "Run a scan to populate the board",
      },
    ];
  }

  return [
    {
      label: "Open alerts",
      value: String(summary.value.openCount),
      note: "Unresolved operational signals",
    },
    {
      label: "Critical",
      value: String(summary.value.criticalCount),
      note: "Immediate intervention required",
    },
    {
      label: "Maintenance due",
      value: String(summary.value.maintenanceCount),
      note: "Date or mileage rules triggered",
    },
    {
      label: "Compliance due",
      value: String(summary.value.complianceCount),
      note: "Vehicle and driver documents expiring",
    },
  ];
});

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

function formatDateTime(value: string): string {
  return new Date(value).toLocaleString();
}

async function loadSummary() {
  if (!session.accessToken) {
    return;
  }

  status.value = "loading";
  errorMessage.value = "";
  try {
    summary.value = normalizeAlertSummaryResponse(
      await apiRequest<AlertSummaryResponse>("/api/v1/alerts/dashboard", {
        token: session.accessToken,
      }),
    );
    status.value = "success";
  } catch {
    status.value = "error";
    errorMessage.value = "Unable to load the operational dashboard.";
  }
}

onMounted(loadSummary);
</script>
