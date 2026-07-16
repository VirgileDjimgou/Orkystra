<template>
  <div class="stacked-page">
    <section class="page-hero">
      <div>
        <span class="eyebrow">Administration</span>
        <h1>Security and data lifecycle</h1>
        <p>
          Prepare your tenant for pilot operations with administrator MFA,
          exportable tenant data, and controlled retention cleanup.
        </p>
      </div>
      <span class="badge text-bg-dark">{{
        session.user?.organizationName
      }}</span>
    </section>

    <div class="row g-4">
      <div class="col-xl-6">
        <section class="surface-panel">
          <div class="panel-heading">
            <div>
              <h2>Administrator MFA</h2>
              <p>Secure the current admin session with an authenticator app.</p>
            </div>
            <button
              class="btn btn-outline-secondary"
              :disabled="isLoadingMfa"
              @click="loadMfaStatus"
            >
              {{ isLoadingMfa ? "Refreshing..." : "Refresh" }}
            </button>
          </div>

          <div class="status-strip">
            <span
              class="badge"
              :class="
                mfaStatus?.isEnabled ? 'text-bg-success' : 'text-bg-warning'
              "
            >
              {{ mfaStatus?.isEnabled ? "Enabled" : "Not enabled" }}
            </span>
            <span class="text-secondary small">
              {{ mfaStatus?.accountEmail || session.user?.email }}
            </span>
          </div>

          <div v-if="mfaMessage.error" class="alert alert-danger">
            {{ mfaMessage.error }}
          </div>
          <div v-if="mfaMessage.success" class="alert alert-success">
            {{ mfaMessage.success }}
          </div>

          <div class="stack-form">
            <button
              class="btn btn-outline-primary"
              :disabled="isSettingUpMfa"
              @click="generateSetup"
            >
              {{
                isSettingUpMfa
                  ? "Generating..."
                  : mfaStatus?.isEnabled
                    ? "Rotate setup secret"
                    : "Generate setup secret"
              }}
            </button>

            <div v-if="mfaSetup" class="secret-banner">
              <strong>Manual setup key</strong>
              <code>{{ mfaSetup.sharedKey }}</code>
              <div class="tiny-meta break-all">
                {{ mfaSetup.authenticatorUri }}
              </div>
            </div>

            <template v-if="mfaSetup && !mfaStatus?.isEnabled">
              <label class="form-label" for="mfaVerifyCode">
                Verification code
              </label>
              <input
                id="mfaVerifyCode"
                v-model="verifyCode"
                class="form-control"
                inputmode="numeric"
                maxlength="8"
                placeholder="123456"
              />
              <button
                class="btn btn-primary"
                :disabled="isVerifyingMfa"
                @click="verifySetup"
              >
                {{ isVerifyingMfa ? "Enabling..." : "Enable MFA" }}
              </button>
            </template>

            <template v-if="mfaStatus?.isEnabled">
              <label class="form-label" for="mfaDisableCode">
                Authenticator code to disable
              </label>
              <input
                id="mfaDisableCode"
                v-model="disableCode"
                class="form-control"
                inputmode="numeric"
                maxlength="8"
                placeholder="123456"
              />
              <button
                class="btn btn-outline-danger"
                :disabled="isDisablingMfa"
                @click="disableMfa"
              >
                {{ isDisablingMfa ? "Disabling..." : "Disable MFA" }}
              </button>
            </template>

            <div v-if="recoveryCodes.length > 0" class="recovery-card">
              <strong>Recovery codes</strong>
              <p>
                Store these one-time codes in your secure admin vault before
                continuing.
              </p>
              <div class="recovery-grid">
                <code v-for="code in recoveryCodes" :key="code">{{
                  code
                }}</code>
              </div>
            </div>
          </div>
        </section>
      </div>

      <div class="col-xl-6">
        <section class="surface-panel">
          <div class="panel-heading">
            <div>
              <h2>Lifecycle summary</h2>
              <p>
                Tenant-scoped counts and retention categories available for
                export or cleanup.
              </p>
            </div>
            <button
              class="btn btn-outline-secondary"
              :disabled="isLoadingSummary"
              @click="loadSummary"
            >
              {{ isLoadingSummary ? "Refreshing..." : "Refresh" }}
            </button>
          </div>

          <div v-if="summaryError" class="alert alert-danger">
            {{ summaryError }}
          </div>
          <div v-else-if="!summary" class="empty-placeholder">
            Loading lifecycle summary...
          </div>
          <div v-else class="summary-grid">
            <article
              v-for="count in summary.counts"
              :key="count.key"
              class="summary-card"
            >
              <strong>{{ count.count }}</strong>
              <span>{{ count.label }}</span>
            </article>
          </div>

          <div v-if="summary" class="retention-note">
            Tracking history retention currently keeps
            <strong>{{ summary.trackingRetentionDays }} day(s)</strong> before
            automatic cleanup on new telemetry ingestion.
          </div>
        </section>
      </div>
    </div>

    <div class="row g-4">
      <div class="col-xl-6">
        <section class="surface-panel">
          <div class="panel-heading">
            <div>
              <h2>Tenant export</h2>
              <p>
                Download the current tenant snapshot before pilot handoff or
                lifecycle deletion.
              </p>
            </div>
          </div>

          <div v-if="exportMessage.error" class="alert alert-danger">
            {{ exportMessage.error }}
          </div>
          <div v-if="exportMessage.success" class="alert alert-success">
            {{ exportMessage.success }}
          </div>

          <button
            class="btn btn-primary"
            :disabled="isExporting"
            @click="downloadExport"
          >
            {{ isExporting ? "Exporting..." : "Download tenant export" }}
          </button>
        </section>
      </div>

      <div class="col-xl-6">
        <section class="surface-panel">
          <div class="panel-heading">
            <div>
              <h2>Controlled purge</h2>
              <p>
                Delete selected historical categories after confirming the
                tenant slug.
              </p>
            </div>
          </div>

          <form class="stack-form" @submit.prevent="purgeLifecycleData">
            <label class="form-label" for="purgeCutoff">Cutoff (UTC)</label>
            <input
              id="purgeCutoff"
              v-model="purgeForm.cutoffLocal"
              class="form-control"
              type="datetime-local"
              required
            />

            <label class="form-label" for="purgeConfirmation">
              Tenant slug confirmation
            </label>
            <input
              id="purgeConfirmation"
              v-model="purgeForm.confirmation"
              class="form-control"
              :placeholder="summary?.organizationSlug ?? 'organization-slug'"
              required
            />

            <fieldset v-if="summary" class="scope-grid">
              <legend>Categories</legend>
              <label
                v-for="category in summary.categories"
                :key="category.key"
                class="scope-option"
              >
                <input
                  v-model="purgeForm.categories"
                  type="checkbox"
                  :value="category.key"
                />
                <span>
                  <strong>{{ category.label }}</strong>
                  <small>{{ category.description }}</small>
                </span>
              </label>
            </fieldset>

            <div v-if="purgeMessage.error" class="alert alert-danger mb-0">
              {{ purgeMessage.error }}
            </div>
            <div v-if="purgeMessage.success" class="alert alert-success mb-0">
              {{ purgeMessage.success }}
            </div>

            <button
              class="btn btn-outline-danger"
              :disabled="isPurging"
              type="submit"
            >
              {{ isPurging ? "Purging..." : "Run controlled purge" }}
            </button>
          </form>
        </section>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from "vue";
import type {
  DataLifecycleSummary,
  MfaSetup,
  MfaStatus,
  PurgeLifecycleDataResponse,
  VerifyMfaResponse,
} from "../features/admin-security/contracts";
import { useSessionStore } from "../features/auth/store";
import { apiRequest } from "../services/api";

const session = useSessionStore();

const mfaStatus = ref<MfaStatus | null>(null);
const mfaSetup = ref<MfaSetup | null>(null);
const recoveryCodes = ref<string[]>([]);
const verifyCode = ref("");
const disableCode = ref("");
const isLoadingMfa = ref(false);
const isSettingUpMfa = ref(false);
const isVerifyingMfa = ref(false);
const isDisablingMfa = ref(false);
const mfaMessage = reactive({ error: "", success: "" });

const summary = ref<DataLifecycleSummary | null>(null);
const isLoadingSummary = ref(false);
const summaryError = ref("");

const isExporting = ref(false);
const exportMessage = reactive({ error: "", success: "" });

const isPurging = ref(false);
const purgeMessage = reactive({ error: "", success: "" });
const purgeForm = reactive({
  cutoffLocal: "",
  confirmation: "",
  categories: [] as string[],
});

function requireToken() {
  if (!session.accessToken) {
    throw new Error("The current session is missing an access token.");
  }

  return session.accessToken;
}

async function loadMfaStatus() {
  const token = requireToken();
  isLoadingMfa.value = true;
  mfaMessage.error = "";
  try {
    mfaStatus.value = await apiRequest<MfaStatus>("/api/admin/security/mfa", {
      token,
    });
  } catch {
    mfaMessage.error = "Unable to load administrator MFA status.";
  } finally {
    isLoadingMfa.value = false;
  }
}

async function generateSetup() {
  const token = requireToken();
  isSettingUpMfa.value = true;
  mfaMessage.error = "";
  mfaMessage.success = "";
  recoveryCodes.value = [];
  try {
    mfaSetup.value = await apiRequest<MfaSetup>(
      "/api/admin/security/mfa/setup",
      {
        method: "POST",
        token,
      },
    );
    verifyCode.value = "";
    disableCode.value = "";
    mfaMessage.success =
      "A fresh authenticator secret is ready for verification.";
    await loadMfaStatus();
  } catch {
    mfaMessage.error = "Unable to generate an authenticator secret.";
  } finally {
    isSettingUpMfa.value = false;
  }
}

async function verifySetup() {
  const token = requireToken();
  isVerifyingMfa.value = true;
  mfaMessage.error = "";
  mfaMessage.success = "";
  try {
    const response = await apiRequest<VerifyMfaResponse>(
      "/api/admin/security/mfa/verify",
      {
        method: "POST",
        token,
        body: { code: verifyCode.value.trim() },
      },
    );
    recoveryCodes.value = response.recoveryCodes;
    verifyCode.value = "";
    mfaMessage.success = "Administrator MFA enabled successfully.";
    await loadMfaStatus();
  } catch {
    mfaMessage.error = "Unable to verify the authenticator code.";
  } finally {
    isVerifyingMfa.value = false;
  }
}

async function disableMfa() {
  const token = requireToken();
  isDisablingMfa.value = true;
  mfaMessage.error = "";
  mfaMessage.success = "";
  try {
    mfaStatus.value = await apiRequest<MfaStatus>(
      "/api/admin/security/mfa/disable",
      {
        method: "POST",
        token,
        body: { code: disableCode.value.trim() },
      },
    );
    disableCode.value = "";
    mfaSetup.value = null;
    recoveryCodes.value = [];
    mfaMessage.success = "Administrator MFA disabled successfully.";
  } catch {
    mfaMessage.error = "Unable to disable administrator MFA.";
  } finally {
    isDisablingMfa.value = false;
  }
}

async function loadSummary() {
  const token = requireToken();
  isLoadingSummary.value = true;
  summaryError.value = "";
  try {
    summary.value = await apiRequest<DataLifecycleSummary>(
      "/api/admin/data-lifecycle/summary",
      { token },
    );
    if (purgeForm.categories.length === 0) {
      purgeForm.categories = summary.value.categories.map(
        (category) => category.key,
      );
    }
    if (!purgeForm.confirmation) {
      purgeForm.confirmation = summary.value.organizationSlug;
    }
  } catch {
    summaryError.value = "Unable to load tenant lifecycle summary.";
  } finally {
    isLoadingSummary.value = false;
  }
}

async function downloadExport() {
  const token = requireToken();
  isExporting.value = true;
  exportMessage.error = "";
  exportMessage.success = "";
  try {
    const payload = await apiRequest<string>(
      "/api/admin/data-lifecycle/export",
      {
        token,
        responseType: "text",
      },
    );
    const blob = new Blob([payload], {
      type: "application/json;charset=utf-8",
    });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = `${summary.value?.organizationSlug ?? "fleetops"}-tenant-export.json`;
    anchor.click();
    URL.revokeObjectURL(url);
    exportMessage.success = "Tenant export generated successfully.";
  } catch {
    exportMessage.error = "Unable to generate the tenant export.";
  } finally {
    isExporting.value = false;
  }
}

async function purgeLifecycleData() {
  const token = requireToken();
  isPurging.value = true;
  purgeMessage.error = "";
  purgeMessage.success = "";
  try {
    const response = await apiRequest<PurgeLifecycleDataResponse>(
      "/api/admin/data-lifecycle/purge",
      {
        method: "POST",
        token,
        body: {
          confirmation: purgeForm.confirmation.trim(),
          cutoffUtc: new Date(purgeForm.cutoffLocal).toISOString(),
          categories: purgeForm.categories,
        },
      },
    );
    purgeMessage.success = `Controlled purge completed: ${response.totalDeleted} item(s) deleted.`;
    await loadSummary();
  } catch {
    purgeMessage.error = "Unable to complete the controlled purge.";
  } finally {
    isPurging.value = false;
  }
}

onMounted(async () => {
  await Promise.all([loadMfaStatus(), loadSummary()]);
});
</script>
