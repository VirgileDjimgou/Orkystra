<template>
  <div class="stacked-page">
    <section class="page-hero">
      <div>
        <span class="eyebrow">Administration</span>
        <h1>Integrations and audit</h1>
        <p>
          Provision partner and device credentials, supervise webhook delivery,
          inspect published contracts, and operate replayable CSV exchanges for
          your tenant.
        </p>
      </div>
      <span class="badge text-bg-dark">{{
        session.user?.organizationName
      }}</span>
    </section>

    <section class="surface-panel">
      <div class="panel-heading">
        <div>
          <h2>Virtual telematics provider</h2>
          <p>
            Operate the tenant-scoped sandbox connection and inspect its
            recovery cursor.
          </p>
        </div>
        <button
          class="btn btn-outline-secondary"
          @click="loadSandboxConnections"
        >
          Refresh
        </button>
      </div>
      <div class="d-flex gap-2 mb-3">
        <input
          v-model="sandboxName"
          class="form-control"
          maxlength="100"
          required
          aria-label="Sandbox connection name"
        />
        <button
          class="btn btn-primary"
          type="button"
          @click="createSandboxConnection"
        >
          Create connection
        </button>
      </div>
      <div v-if="sandboxError" class="alert alert-danger">
        {{ sandboxError }}
      </div>
      <div v-if="sandboxConnections.length === 0" class="empty-placeholder">
        No virtual provider connection configured.
      </div>
      <div v-else class="user-list">
        <article
          v-for="connection in sandboxConnections"
          :key="connection.id"
          class="user-card"
        >
          <div>
            <strong>{{ connection.name }}</strong>
            <div class="tiny-meta">
              Cursor: {{ connection.resumeCursor || "Not started" }}
            </div>
            <div class="tiny-meta">{{ connection.lastError || "Healthy" }}</div>
          </div>
          <div class="user-meta">
            <div :class="connection.isActive ? 'text-success' : 'text-danger'">
              {{ connection.isActive ? "Active" : "Disabled" }}
            </div>
            <div>
              <small>{{ formatDateTime(connection.lastSucceededAtUtc) }}</small>
            </div>
            <div>
              <button
                class="btn btn-outline-secondary btn-sm"
                @click="setSandboxConnection(connection, !connection.isActive)"
              >
                {{ connection.isActive ? "Disable" : "Enable" }}
              </button>
            </div>
          </div>
        </article>
      </div>
    </section>

    <div class="row g-4">
      <div class="col-xl-7">
        <section class="surface-panel">
          <div class="panel-heading">
            <div>
              <h2>API credentials</h2>
              <p>Issue scoped secrets for partners and devices.</p>
            </div>
            <button
              class="btn btn-outline-secondary"
              :disabled="isRefreshingCredentials"
              @click="loadCredentials"
            >
              {{ isRefreshingCredentials ? "Refreshing..." : "Refresh" }}
            </button>
          </div>

          <div v-if="credentialsError" class="alert alert-danger">
            {{ credentialsError }}
          </div>
          <div
            v-else-if="isLoadingCredentials && apiKeys.length === 0"
            class="empty-placeholder"
          >
            Loading API credentials...
          </div>
          <div v-else-if="apiKeys.length === 0" class="empty-placeholder">
            No API credentials issued yet.
          </div>
          <div v-else class="user-list">
            <article
              v-for="credential in apiKeys"
              :key="credential.id"
              class="user-card"
            >
              <div>
                <strong>{{ credential.name }}</strong>
                <div class="text-secondary small">
                  {{ credential.credentialType }} · {{ credential.keyId }}
                </div>
                <div class="tiny-meta">
                  Scopes: {{ credential.scopes.join(", ") || "None" }}
                </div>
                <div class="tiny-meta">
                  Secret preview: <code>{{ credential.secretPreview }}</code>
                </div>
              </div>
              <div class="user-meta">
                <span
                  :class="credential.isActive ? 'text-success' : 'text-danger'"
                >
                  {{ credential.isActive ? "Active" : "Revoked" }}
                </span>
                <small v-if="credential.lastUsedAtUtc" class="text-secondary">
                  Used {{ formatDateTime(credential.lastUsedAtUtc) }}
                </small>
                <button
                  v-if="credential.isActive"
                  class="btn btn-outline-danger btn-sm"
                  :disabled="busyCredentialId === credential.id"
                  @click="revokeCredential(credential.id)"
                >
                  Revoke
                </button>
              </div>
            </article>
          </div>

          <div v-if="createdSecret" class="secret-banner">
            <strong>Copy this secret now.</strong>
            <p>
              It will not be shown again. Store it in your secure partner vault.
            </p>
            <code>{{ createdSecret }}</code>
          </div>
        </section>
      </div>

      <div class="col-xl-5">
        <section class="surface-panel">
          <div class="panel-heading">
            <div>
              <h2>Issue a credential</h2>
              <p>
                Scopes are enforced server-side and visible before creation.
              </p>
            </div>
          </div>

          <form class="stack-form" @submit.prevent="createCredential">
            <label class="form-label" for="credentialName">Name</label>
            <input
              id="credentialName"
              v-model="credentialForm.name"
              class="form-control"
              maxlength="120"
              required
            />

            <!-- prettier-ignore -->
            <label class="form-label" for="credentialType">Credential type</label>
            <select
              id="credentialType"
              v-model="credentialForm.credentialType"
              class="form-select"
            >
              <option value="Partner">Partner</option>
              <option value="Device">Device</option>
            </select>

            <fieldset class="scope-grid">
              <legend>Scopes</legend>
              <label
                v-for="scope in availableScopes"
                :key="scope.value"
                class="scope-option"
              >
                <input
                  v-model="credentialForm.scopes"
                  type="checkbox"
                  :value="scope.value"
                />
                <span>
                  <strong>{{ scope.label }}</strong>
                  <small>{{ scope.hint }}</small>
                </span>
              </label>
            </fieldset>

            <div v-if="credentialMessage.error" class="alert alert-danger mb-0">
              {{ credentialMessage.error }}
            </div>
            <div
              v-if="credentialMessage.success"
              class="alert alert-success mb-0"
            >
              {{ credentialMessage.success }}
            </div>

            <button
              class="btn btn-primary"
              type="submit"
              :disabled="isSubmittingCredential"
            >
              {{ isSubmittingCredential ? "Issuing..." : "Issue credential" }}
            </button>
          </form>
        </section>
      </div>
    </div>

    <div class="row g-4">
      <div class="col-xl-7">
        <section class="surface-panel">
          <div class="panel-heading">
            <div>
              <h2>Webhook endpoints</h2>
              <p>
                Supervise delivery targets, retry status, and sandbox routing.
              </p>
            </div>
            <button
              class="btn btn-outline-secondary"
              :disabled="isRefreshingWebhooks"
              @click="loadWebhooks"
            >
              {{ isRefreshingWebhooks ? "Refreshing..." : "Refresh" }}
            </button>
          </div>

          <div v-if="webhooksError" class="alert alert-danger">
            {{ webhooksError }}
          </div>
          <div
            v-else-if="isLoadingWebhooks && webhooks.length === 0"
            class="empty-placeholder"
          >
            Loading webhook endpoints...
          </div>
          <div v-else-if="webhooks.length === 0" class="empty-placeholder">
            No webhooks registered for this tenant.
          </div>
          <div v-else class="user-list">
            <article
              v-for="webhook in webhooks"
              :key="webhook.id"
              class="user-card"
            >
              <div>
                <strong>{{ webhook.name }}</strong>
                <div class="text-secondary small">{{ webhook.eventType }}</div>
                <div class="tiny-meta break-all">{{ webhook.targetUrl }}</div>
                <div class="tiny-meta">
                  {{
                    webhook.isSandbox
                      ? "Sandbox receiver"
                      : "External delivery target"
                  }}
                </div>
              </div>
              <div class="user-meta">
                <span
                  :class="webhook.isActive ? 'text-success' : 'text-danger'"
                >
                  {{ webhook.isActive ? "Active" : "Disabled" }}
                </span>
                <small v-if="webhook.lastSucceededAtUtc" class="text-secondary">
                  Last success {{ formatDateTime(webhook.lastSucceededAtUtc) }}
                </small>
                <button
                  v-if="webhook.isActive"
                  class="btn btn-outline-danger btn-sm"
                  :disabled="busyWebhookId === webhook.id"
                  @click="disableWebhook(webhook.id)"
                >
                  Disable
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
              <h2>Create a webhook</h2>
              <p>Use sandbox mode to validate HMAC payloads safely.</p>
            </div>
          </div>

          <form class="stack-form" @submit.prevent="createWebhook">
            <label class="form-label" for="webhookName">Name</label>
            <input
              id="webhookName"
              v-model="webhookForm.name"
              class="form-control"
              maxlength="120"
              required
            />

            <label class="form-label" for="webhookEvent">Event contract</label>
            <select
              id="webhookEvent"
              v-model="webhookForm.eventType"
              class="form-select"
            >
              <option
                v-for="eventType in availableEventTypes"
                :key="eventType"
                :value="eventType"
              >
                {{ eventType }}
              </option>
            </select>

            <label class="form-label" for="signingSecret">Signing secret</label>
            <input
              id="signingSecret"
              v-model="webhookForm.signingSecret"
              class="form-control"
              maxlength="120"
              required
            />

            <label class="sandbox-toggle">
              <input v-model="webhookForm.isSandbox" type="checkbox" />
              <span>Use FleetOps sandbox receiver</span>
            </label>

            <template v-if="!webhookForm.isSandbox">
              <label class="form-label" for="targetUrl">Target URL</label>
              <input
                id="targetUrl"
                v-model="webhookForm.targetUrl"
                class="form-control"
                type="url"
                placeholder="https://partner.example/webhooks/fleetops"
                required
              />
            </template>

            <div v-if="webhookMessage.error" class="alert alert-danger mb-0">
              {{ webhookMessage.error }}
            </div>
            <div v-if="webhookMessage.success" class="alert alert-success mb-0">
              {{ webhookMessage.success }}
            </div>

            <button
              class="btn btn-primary"
              type="submit"
              :disabled="isSubmittingWebhook"
            >
              {{ isSubmittingWebhook ? "Creating..." : "Create webhook" }}
            </button>
          </form>
        </section>
      </div>
    </div>

    <div class="row g-4">
      <div class="col-xl-6">
        <section class="surface-panel">
          <div class="panel-heading">
            <div>
              <h2>Published contracts</h2>
              <p>Documented event payloads available for partners today.</p>
            </div>
            <button
              class="btn btn-outline-secondary"
              :disabled="isLoadingContracts"
              @click="loadContracts"
            >
              {{ isLoadingContracts ? "Refreshing..." : "Refresh" }}
            </button>
          </div>

          <div v-if="contractsError" class="alert alert-danger">
            {{ contractsError }}
          </div>
          <div
            v-else-if="isLoadingContracts && contracts.length === 0"
            class="empty-placeholder"
          >
            Loading contracts...
          </div>
          <div v-else-if="contracts.length === 0" class="empty-placeholder">
            No contracts published.
          </div>
          <div v-else class="contract-list">
            <article
              v-for="contract in contracts"
              :key="contract.eventType"
              class="contract-card"
            >
              <strong>{{ contract.eventType }}</strong>
              <p>{{ contract.description }}</p>
              <pre>{{ formatExample(contract.examplePayload) }}</pre>
            </article>
          </div>
        </section>
      </div>

      <div class="col-xl-6">
        <section class="surface-panel">
          <div class="panel-heading">
            <div>
              <h2>CSV exchange desk</h2>
              <p>
                Replay safe imports and downloadable exports per fleet domain.
              </p>
            </div>
          </div>

          <div class="csv-actions">
            <article
              v-for="resource in csvResources"
              :key="resource.key"
              class="csv-resource-card"
            >
              <div>
                <strong>{{ resource.label }}</strong>
                <p>{{ resource.description }}</p>
              </div>
              <div class="csv-resource-actions">
                <button
                  class="btn btn-outline-secondary btn-sm"
                  :disabled="downloadingResource === resource.key"
                  @click="downloadCsv(resource.key)"
                >
                  {{
                    downloadingResource === resource.key
                      ? "Exporting..."
                      : "Export CSV"
                  }}
                </button>
                <button
                  class="btn btn-outline-primary btn-sm"
                  @click="selectedImportResource = resource.key"
                >
                  Load import template
                </button>
              </div>
            </article>
          </div>

          <form
            data-testid="csv-import-form"
            class="stack-form mt-4"
            @submit.prevent="submitImport"
          >
            <label class="form-label" for="importResource">Import target</label>
            <select
              id="importResource"
              v-model="selectedImportResource"
              class="form-select"
            >
              <option
                v-for="resource in csvResources"
                :key="resource.key"
                :value="resource.key"
              >
                {{ resource.label }}
              </option>
            </select>

            <label class="form-label" for="csvBody">CSV payload</label>
            <textarea
              id="csvBody"
              v-model="importBody"
              class="form-control"
              rows="6"
              required
            />

            <div v-if="csvMessage.error" class="alert alert-danger mb-0">
              {{ csvMessage.error }}
            </div>
            <div v-if="csvMessage.success" class="alert alert-success mb-0">
              {{ csvMessage.success }}
            </div>

            <button
              class="btn btn-primary"
              type="submit"
              :disabled="isImportingCsv"
            >
              {{ isImportingCsv ? "Importing..." : "Run import" }}
            </button>
          </form>
        </section>
      </div>
    </div>

    <section class="surface-panel">
      <div class="panel-heading">
        <div>
          <h2>Delivery outbox</h2>
          <p>
            Recent webhook publications, retries, and dead-letter visibility.
          </p>
        </div>
        <button
          class="btn btn-outline-secondary"
          :disabled="isLoadingOutbox"
          @click="loadOutbox"
        >
          {{ isLoadingOutbox ? "Refreshing..." : "Refresh" }}
        </button>
      </div>

      <div v-if="outboxError" class="alert alert-danger">
        {{ outboxError }}
      </div>
      <div
        v-else-if="isLoadingOutbox && outbox.length === 0"
        class="empty-placeholder"
      >
        Loading delivery outbox...
      </div>
      <div v-else-if="outbox.length === 0" class="empty-placeholder">
        No outbox activity recorded yet.
      </div>
      <div v-else class="table-responsive">
        <table class="table align-middle">
          <thead>
            <tr>
              <th>Event</th>
              <th>Status</th>
              <th>Attempts</th>
              <th>Occurred</th>
              <th>Last error</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="item in outbox" :key="item.id">
              <td>
                <strong>{{ item.eventType }}</strong>
                <div class="text-secondary small">
                  {{ item.aggregateType }} · {{ item.aggregateId }}
                </div>
              </td>
              <td>{{ item.status }}</td>
              <td>{{ item.attemptCount }}</td>
              <td>{{ formatDateTime(item.occurredAtUtc) }}</td>
              <td class="table-error-cell">
                {{ item.lastError || "None" }}
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </section>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from "vue";
import { useSessionStore } from "../features/auth/store";
import { apiRequest } from "../services/api";
import type {
  ApiClientCredential,
  ApiClientCredentialType,
  CreateApiClientCredentialRequest,
  CreateWebhookEndpointRequest,
  CreatedApiClientCredential,
  IntegrationContract,
  IntegrationOutboxMessage,
  SandboxTelematicsConnection,
  WebhookEndpoint,
} from "../features/integrations/contracts";

type CsvResourceKey = "vehicles" | "drivers" | "devices";
type ImportSummary = {
  created: number;
  updated: number;
  skipped: number;
  errors: string[];
};

const session = useSessionStore();

const apiKeys = ref<ApiClientCredential[]>([]);
const webhooks = ref<WebhookEndpoint[]>([]);
const contracts = ref<IntegrationContract[]>([]);
const outbox = ref<IntegrationOutboxMessage[]>([]);
const sandboxConnections = ref<SandboxTelematicsConnection[]>([]);
const sandboxName = ref("FleetOps virtual provider");
const sandboxError = ref("");

const isLoadingCredentials = ref(false);
const isRefreshingCredentials = ref(false);
const isSubmittingCredential = ref(false);
const credentialsError = ref("");
const createdSecret = ref("");
const busyCredentialId = ref<string | null>(null);
const credentialMessage = reactive({ error: "", success: "" });

const isLoadingWebhooks = ref(false);
const isRefreshingWebhooks = ref(false);
const isSubmittingWebhook = ref(false);
const webhooksError = ref("");
const busyWebhookId = ref<string | null>(null);
const webhookMessage = reactive({ error: "", success: "" });

const isLoadingContracts = ref(false);
const contractsError = ref("");

const isLoadingOutbox = ref(false);
const outboxError = ref("");

const isImportingCsv = ref(false);
const downloadingResource = ref<CsvResourceKey | null>(null);
const csvMessage = reactive({ error: "", success: "" });

const credentialForm = reactive<CreateApiClientCredentialRequest>({
  name: "Northwind partner feed",
  credentialType: "Partner",
  scopes: ["partner-fleet-read"],
});

const webhookForm = reactive<CreateWebhookEndpointRequest>({
  name: "Sandbox fleet observer",
  eventType: "fleet.vehicle.created",
  targetUrl: null,
  signingSecret: "sandbox-secret",
  isSandbox: true,
});

const csvResources = [
  {
    key: "vehicles" as const,
    label: "Vehicles",
    description: "Replayable upsert for registrations and display names.",
    exportPath: "/api/v1/fleet/vehicles/export",
    importPath: "/api/v1/fleet/vehicles/import",
    template: "registrationNumber,displayName\nNW-300,City service van",
    fileName: "fleetops-vehicles.csv",
  },
  {
    key: "drivers" as const,
    label: "Drivers",
    description: "Replayable upsert for staff identity and contact details.",
    exportPath: "/api/v1/fleet/drivers/export",
    importPath: "/api/v1/fleet/drivers/import",
    template:
      "fullName,licenseNumber,phoneNumber\nAlex North,NW-DL-300,+1-555-0100",
    fileName: "fleetops-drivers.csv",
  },
  {
    key: "devices" as const,
    label: "Devices",
    description: "Replayable upsert for telematics serials and display names.",
    exportPath: "/api/v1/fleet/devices/export",
    importPath: "/api/v1/fleet/devices/import",
    template: "serialNumber,displayName\nNW-GPS-300,Reserve tracker",
    fileName: "fleetops-devices.csv",
  },
];

const selectedImportResource = ref<CsvResourceKey>("vehicles");
const importBody = ref(csvResources[0].template);
const availableEventTypes = computed(() =>
  contracts.value.length > 0
    ? contracts.value.map((contract) => contract.eventType)
    : [
        "fleet.vehicle.created",
        "dispatch.mission.status-changed",
        "alerts.opened",
      ],
);

const availableScopes = computed(() => {
  if (credentialForm.credentialType === "Partner") {
    return [
      {
        value: "partner-fleet-read",
        label: "Partner fleet read",
        hint: "Read scoped fleet inventory for external systems.",
      },
      {
        value: "partner-webhook-read",
        label: "Partner webhook read",
        hint: "Inspect delivery status from partner-side consoles.",
      },
    ];
  }

  return [
    {
      value: "device-tracking-write",
      label: "Device tracking write",
      hint: "Submit telemetry events from approved trackers.",
    },
  ];
});

watch(
  () => credentialForm.credentialType,
  (type: ApiClientCredentialType) => {
    credentialForm.scopes =
      type === "Partner" ? ["partner-fleet-read"] : ["device-tracking-write"];
  },
  { immediate: false },
);

watch(selectedImportResource, (resourceKey) => {
  const resource = csvResources.find((item) => item.key === resourceKey);
  if (resource) {
    importBody.value = resource.template;
  }
});

function requireToken() {
  if (!session.accessToken) {
    throw new Error("The current session is missing an access token.");
  }

  return session.accessToken;
}

function formatDateTime(value: string | null) {
  if (!value) {
    return "Never";
  }

  return new Intl.DateTimeFormat("en-GB", {
    dateStyle: "medium",
    timeStyle: "short",
    timeZone: "UTC",
  }).format(new Date(value));
}

async function loadSandboxConnections() {
  try {
    sandboxConnections.value = await apiRequest<SandboxTelematicsConnection[]>(
      "/api/v1/admin/integrations/sandbox-telematics",
      { token: requireToken() },
    );
  } catch {
    sandboxError.value = "Unable to load virtual telematics connections.";
  }
}

async function createSandboxConnection() {
  try {
    await apiRequest("/api/v1/admin/integrations/sandbox-telematics", {
      method: "POST",
      token: requireToken(),
      body: { name: sandboxName.value },
    });
    await loadSandboxConnections();
  } catch {
    sandboxError.value = "Unable to create the virtual telematics connection.";
  }
}

async function setSandboxConnection(
  connection: SandboxTelematicsConnection,
  enabled: boolean,
) {
  try {
    await apiRequest(
      `/api/v1/admin/integrations/sandbox-telematics/${connection.id}/enable?enabled=${enabled}`,
      { method: "POST", token: requireToken() },
    );
    await loadSandboxConnections();
  } catch {
    sandboxError.value = "Unable to update the virtual telematics connection.";
  }
}

function formatExample(payload: Record<string, unknown>) {
  return JSON.stringify(payload, null, 2);
}

async function loadCredentials() {
  const token = requireToken();
  credentialsError.value = "";
  isRefreshingCredentials.value = apiKeys.value.length > 0;
  isLoadingCredentials.value = apiKeys.value.length === 0;
  try {
    apiKeys.value = await apiRequest<ApiClientCredential[]>(
      "/api/v1/admin/integrations/api-keys",
      { token },
    );
  } catch {
    credentialsError.value = "Unable to load API credentials.";
  } finally {
    isLoadingCredentials.value = false;
    isRefreshingCredentials.value = false;
  }
}

async function createCredential() {
  const token = requireToken();
  isSubmittingCredential.value = true;
  credentialMessage.error = "";
  credentialMessage.success = "";
  createdSecret.value = "";
  try {
    const created = await apiRequest<CreatedApiClientCredential>(
      "/api/v1/admin/integrations/api-keys",
      {
        method: "POST",
        token,
        body: {
          ...credentialForm,
          name: credentialForm.name.trim(),
        } satisfies CreateApiClientCredentialRequest,
      },
    );
    createdSecret.value = created.plainTextSecret;
    credentialMessage.success = `Credential ${created.name} issued successfully.`;
    credentialForm.name = "";
    await loadCredentials();
  } catch {
    credentialMessage.error = "Unable to issue the selected credential.";
  } finally {
    isSubmittingCredential.value = false;
  }
}

async function revokeCredential(credentialId: string) {
  const token = requireToken();
  busyCredentialId.value = credentialId;
  credentialMessage.error = "";
  credentialMessage.success = "";
  try {
    await apiRequest<ApiClientCredential>(
      `/api/v1/admin/integrations/api-keys/${credentialId}/revoke`,
      {
        method: "POST",
        token,
      },
    );
    credentialMessage.success = "Credential revoked successfully.";
    await loadCredentials();
  } catch {
    credentialMessage.error = "Unable to revoke the selected credential.";
  } finally {
    busyCredentialId.value = null;
  }
}

async function loadWebhooks() {
  const token = requireToken();
  webhooksError.value = "";
  isRefreshingWebhooks.value = webhooks.value.length > 0;
  isLoadingWebhooks.value = webhooks.value.length === 0;
  try {
    webhooks.value = await apiRequest<WebhookEndpoint[]>(
      "/api/v1/admin/integrations/webhooks",
      { token },
    );
  } catch {
    webhooksError.value = "Unable to load webhook endpoints.";
  } finally {
    isLoadingWebhooks.value = false;
    isRefreshingWebhooks.value = false;
  }
}

async function createWebhook() {
  const token = requireToken();
  isSubmittingWebhook.value = true;
  webhookMessage.error = "";
  webhookMessage.success = "";
  try {
    const created = await apiRequest<WebhookEndpoint>(
      "/api/v1/admin/integrations/webhooks",
      {
        method: "POST",
        token,
        body: {
          ...webhookForm,
          name: webhookForm.name.trim(),
          targetUrl: webhookForm.isSandbox
            ? null
            : webhookForm.targetUrl?.trim() || null,
        } satisfies CreateWebhookEndpointRequest,
      },
    );
    webhookMessage.success = `Webhook ${created.name} created successfully.`;
    webhookForm.name = "";
    webhookForm.signingSecret = "";
    webhookForm.targetUrl = null;
    await loadWebhooks();
    await loadOutbox();
  } catch {
    webhookMessage.error = "Unable to create the selected webhook.";
  } finally {
    isSubmittingWebhook.value = false;
  }
}

async function disableWebhook(webhookId: string) {
  const token = requireToken();
  busyWebhookId.value = webhookId;
  webhookMessage.error = "";
  webhookMessage.success = "";
  try {
    await apiRequest<WebhookEndpoint>(
      `/api/v1/admin/integrations/webhooks/${webhookId}/disable`,
      {
        method: "POST",
        token,
      },
    );
    webhookMessage.success = "Webhook disabled successfully.";
    await loadWebhooks();
  } catch {
    webhookMessage.error = "Unable to disable the selected webhook.";
  } finally {
    busyWebhookId.value = null;
  }
}

async function loadContracts() {
  const token = requireToken();
  contractsError.value = "";
  isLoadingContracts.value = true;
  try {
    contracts.value = await apiRequest<IntegrationContract[]>(
      "/api/v1/admin/integrations/contracts",
      { token },
    );
    if (!availableEventTypes.value.includes(webhookForm.eventType)) {
      webhookForm.eventType =
        availableEventTypes.value[0] ?? webhookForm.eventType;
    }
  } catch {
    contractsError.value = "Unable to load integration contracts.";
  } finally {
    isLoadingContracts.value = false;
  }
}

async function loadOutbox() {
  const token = requireToken();
  outboxError.value = "";
  isLoadingOutbox.value = true;
  try {
    outbox.value = await apiRequest<IntegrationOutboxMessage[]>(
      "/api/v1/admin/integrations/outbox",
      { token },
    );
  } catch {
    outboxError.value = "Unable to load delivery outbox.";
  } finally {
    isLoadingOutbox.value = false;
  }
}

async function downloadCsv(resourceKey: CsvResourceKey) {
  const token = requireToken();
  const resource = csvResources.find((item) => item.key === resourceKey);
  if (!resource) {
    return;
  }

  downloadingResource.value = resourceKey;
  csvMessage.error = "";
  csvMessage.success = "";
  try {
    const csv = await apiRequest<string>(resource.exportPath, {
      token,
      responseType: "text",
    });
    const blob = new Blob([csv], { type: "text/csv;charset=utf-8" });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = resource.fileName;
    anchor.click();
    URL.revokeObjectURL(url);
    csvMessage.success = `${resource.label} export generated successfully.`;
  } catch {
    csvMessage.error = `Unable to export ${resource.label.toLowerCase()} CSV.`;
  } finally {
    downloadingResource.value = null;
  }
}

async function submitImport() {
  const token = requireToken();
  const resource = csvResources.find(
    (item) => item.key === selectedImportResource.value,
  );
  if (!resource) {
    return;
  }

  isImportingCsv.value = true;
  csvMessage.error = "";
  csvMessage.success = "";
  try {
    const summary = await apiRequest<ImportSummary>(resource.importPath, {
      method: "POST",
      token,
      body: importBody.value,
      contentType: "text/csv",
    });
    csvMessage.success = `${resource.label} import complete: ${summary.created} created, ${summary.updated} updated, ${summary.skipped} skipped.`;
  } catch {
    csvMessage.error = `Unable to import ${resource.label.toLowerCase()} CSV payload.`;
  } finally {
    isImportingCsv.value = false;
  }
}

onMounted(async () => {
  await Promise.all([
    loadCredentials(),
    loadWebhooks(),
    loadContracts(),
    loadOutbox(),
    loadSandboxConnections(),
  ]);
});
</script>
