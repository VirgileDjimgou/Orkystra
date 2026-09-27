<template>
  <section class="demo-control-bar" aria-label="Simulated demo controls">
    <strong>SIMULATED DEMO</strong>
    <label>
      Scenario
      <select v-model="scenario" class="form-select form-select-sm">
        <option v-for="option in scenarios" :key="option" :value="option">
          {{ option.replaceAll("_", " ") }}
        </option>
      </select>
    </label>
    <span class="demo-control-status">{{ state?.status ?? "READY" }}</span>
    <button class="btn btn-sm btn-light" type="button" @click="apply('START')">
      Start
    </button>
    <button class="btn btn-sm btn-light" type="button" @click="apply('PAUSE')">
      Pause
    </button>
    <button
      class="btn btn-sm btn-outline-light"
      type="button"
      @click="apply('RESET')"
    >
      Reset
    </button>
  </section>
</template>

<script setup lang="ts">
import { onMounted, ref } from "vue";
import { useRouter } from "vue-router";
import type { DemoSessionState } from "../auth/contracts";
import { apiRequest } from "../../services/api";

const router = useRouter();
const scenarios = [
  "NORMAL_SHIFT",
  "LATE_DELIVERY",
  "VEHICLE_ISSUE",
  "DRIVER_CONNECTIVITY_LOSS",
  "COMPLIANCE_WARNING",
];
const scenario = ref("NORMAL_SHIFT");
const state = ref<DemoSessionState | null>(null);

onMounted(load);

async function load() {
  try {
    state.value = await apiRequest<DemoSessionState>(
      "/api/v1/demo/session/control",
    );
    scenario.value = state.value.scenario;
  } catch {
    await router.push({ path: "/demo", query: { expired: "1" } });
  }
}

async function apply(action: string) {
  try {
    state.value = await apiRequest<DemoSessionState>(
      "/api/v1/demo/session/control",
      {
        method: "POST",
        body: { action, scenario: scenario.value },
      },
    );
  } catch {
    await router.push({ path: "/demo", query: { expired: "1" } });
  }
}
</script>
