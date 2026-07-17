<!-- eslint-disable vue/html-closing-bracket-newline, vue/html-indent, vue/multiline-html-element-content-newline -->
<template>
  <div class="stacked-page">
    <section class="page-hero">
      <div>
        <span class="eyebrow">Dispatch productivity</span>
        <h1>Daily planning workspace</h1>
        <p>
          Preview imports, reuse route templates, and make explicit assignment
          decisions.
        </p>
      </div>
      <button
        class="btn btn-outline-secondary"
        type="button"
        @click="loadBoard"
      >
        Refresh board
      </button>
    </section>

    <div
      v-if="message"
      class="alert"
      :class="messageError ? 'alert-danger' : 'alert-success'"
      role="status"
    >
      {{ message }}
    </div>
    <div class="row g-4">
      <section class="col-lg-7">
        <div class="surface-panel">
          <div class="panel-heading">
            <div>
              <h2>Day / week board</h2>
              <p>Capacity-aware, paged mission view.</p>
            </div>
          </div>
          <label class="form-label" for="board-date">Starting date</label
          ><input
            id="board-date"
            v-model="date"
            class="form-control mb-3"
            type="date"
            @change="loadBoard"
          />
          <div class="table-responsive">
            <table class="table align-middle">
              <thead>
                <tr>
                  <th>Mission</th>
                  <th>Window</th>
                  <th>Assignment</th>
                  <th>Status</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="mission in board.items" :key="mission.id">
                  <td>
                    <strong>{{ mission.reference }}</strong
                    ><br /><small>{{ mission.title }}</small>
                  </td>
                  <td>
                    {{ format(mission.scheduledStartUtc) }} –
                    {{ format(mission.scheduledEndUtc) }}
                  </td>
                  <td>
                    {{ mission.driverName ?? "Unassigned" }}<br /><small>{{
                      mission.vehicleRegistrationNumber ?? "No vehicle"
                    }}</small>
                  </td>
                  <td>{{ mission.status }}</td>
                </tr>
                <tr v-if="!board.items.length">
                  <td colspan="4" class="text-secondary">
                    No missions in this period.
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </section>
      <section class="col-lg-5">
        <div class="surface-panel">
          <div class="panel-heading">
            <div>
              <h2>Route templates</h2>
              <p>Create a reusable draft route.</p>
            </div>
          </div>
          <form class="stack-form" @submit.prevent="createTemplate">
            <input
              v-model="template.name"
              class="form-control"
              placeholder="Template name"
              required
              maxlength="100"
            /><input
              v-model="template.title"
              class="form-control"
              placeholder="Mission title"
              required
              maxlength="160"
            /><input
              v-model="template.stopName"
              class="form-control"
              placeholder="First stop name"
              required
            /><input
              v-model="template.stopAddress"
              class="form-control"
              placeholder="First stop address"
              required
            /><button class="btn btn-primary" type="submit">
              Save template
            </button>
          </form>
          <hr />
          <ul class="list-group list-group-flush">
            <li
              v-for="item in templates"
              :key="item.id"
              class="list-group-item d-flex justify-content-between"
            >
              <span
                ><strong>{{ item.name }}</strong
                ><br /><small>{{ item.stops.length }} stop(s)</small></span
              ><button
                class="btn btn-sm btn-outline-primary"
                type="button"
                @click="duplicate(item.id)"
              >
                Duplicate
              </button>
            </li>
          </ul>
        </div>
      </section>
      <section class="col-12">
        <div class="surface-panel">
          <div class="panel-heading">
            <div>
              <h2>Idempotent import</h2>
              <p>
                Paste a JSON array of dispatch rows, preview errors, then
                confirm the exact import.
              </p>
            </div>
          </div>
          <form class="stack-form" @submit.prevent="previewImport">
            <input
              v-model="importKey"
              class="form-control"
              placeholder="Unique import key"
              required
            /><textarea
              v-model="importRows"
              class="form-control"
              rows="5"
              aria-label="Import rows JSON"
            ></textarea>
            <div class="d-flex gap-2">
              <button class="btn btn-outline-secondary" type="submit">
                Preview</button
              ><button
                class="btn btn-primary"
                type="button"
                :disabled="!preview || preview.invalidRows > 0"
                @click="confirmImport"
              >
                Confirm import
              </button>
            </div>
          </form>
          <p v-if="preview" class="mt-3 mb-0">
            {{ preview.validRows }} valid row(s),
            {{ preview.invalidRows }} error(s){{
              preview.alreadyImported
                ? "; this key has already been applied."
                : ""
            }}
          </p>
          <ul v-if="preview?.errors.length" class="mt-2 mb-0">
            <li v-for="error in preview.errors" :key="error">{{ error }}</li>
          </ul>
        </div>
      </section>
    </div>
  </div>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from "vue";
import { useSessionStore } from "../features/auth/store";
import { apiRequest } from "../services/api";

type Board = {
  totalCount: number;
  items: Array<{
    id: string;
    reference: string;
    title: string;
    status: string;
    scheduledStartUtc: string;
    scheduledEndUtc: string;
    driverName?: string;
    vehicleRegistrationNumber?: string;
  }>;
};
type Template = { id: string; name: string; title: string; stops: unknown[] };
type Preview = {
  validRows: number;
  invalidRows: number;
  errors: string[];
  alreadyImported: boolean;
};
const session = useSessionStore();
const board = reactive<Board>({ totalCount: 0, items: [] });
const templates = ref<Template[]>([]);
const date = ref(new Date().toISOString().slice(0, 10));
const message = ref("");
const messageError = ref(false);
const importKey = ref("");
const preview = ref<Preview | null>(null);
const template = reactive({
  name: "",
  title: "",
  stopName: "",
  stopAddress: "",
});
const importRows = ref("[]");
const token = () => session.accessToken;
const notify = (value: string, error = false) => {
  message.value = value;
  messageError.value = error;
};
const format = (value: string) => new Date(value).toLocaleString();
async function loadBoard() {
  try {
    const start = new Date(`${date.value}T00:00:00Z`);
    const end = new Date(start);
    end.setUTCDate(end.getUTCDate() + 7);
    const result = await apiRequest<Board>(
      `/api/v1/dispatch/board?fromUtc=${encodeURIComponent(start.toISOString())}&toUtc=${encodeURIComponent(end.toISOString())}`,
      { token: token() },
    );
    board.totalCount = result.totalCount;
    board.items = result.items;
  } catch {
    notify("Unable to load the dispatch board.", true);
  }
}
async function loadTemplates() {
  try {
    templates.value = await apiRequest<Template[]>(
      "/api/v1/dispatch/templates",
      { token: token() },
    );
  } catch {
    notify("Unable to load templates.", true);
  }
}
async function createTemplate() {
  try {
    await apiRequest("/api/v1/dispatch/templates", {
      method: "POST",
      token: token(),
      body: {
        name: template.name,
        title: template.title,
        stops: [
          {
            sequence: 1,
            name: template.stopName,
            address: template.stopAddress,
            arrivalOffsetMinutes: 0,
          },
        ],
      },
    });
    Object.assign(template, {
      name: "",
      title: "",
      stopName: "",
      stopAddress: "",
    });
    await loadTemplates();
    notify("Template saved.");
  } catch {
    notify("Unable to save the template.", true);
  }
}
async function duplicate(id: string) {
  const start = new Date(`${date.value}T08:00:00Z`);
  const end = new Date(start);
  end.setHours(end.getHours() + 1);
  try {
    await apiRequest(`/api/v1/dispatch/templates/${id}/duplicate`, {
      method: "POST",
      token: token(),
      body: {
        reference: `TPL-${Date.now()}`,
        scheduledStartUtc: start.toISOString(),
        scheduledEndUtc: end.toISOString(),
      },
    });
    await loadBoard();
    notify("Draft mission created from template.");
  } catch {
    notify("Unable to duplicate the template.", true);
  }
}
function requestBody() {
  return { importKey: importKey.value, rows: JSON.parse(importRows.value) };
}
async function previewImport() {
  try {
    preview.value = await apiRequest<Preview>(
      "/api/v1/dispatch/imports/preview",
      { method: "POST", token: token(), body: requestBody() },
    );
  } catch {
    notify("The import JSON is invalid or could not be previewed.", true);
  }
}
async function confirmImport() {
  if (
    !window.confirm("Create the previewed missions? This action is recorded.")
  )
    return;
  try {
    await apiRequest("/api/v1/dispatch/imports/confirm", {
      method: "POST",
      token: token(),
      body: requestBody(),
    });
    await loadBoard();
    notify("Import confirmed.");
  } catch {
    notify("Unable to confirm the import.", true);
  }
}
onMounted(async () => {
  await Promise.all([loadBoard(), loadTemplates()]);
});
</script>
