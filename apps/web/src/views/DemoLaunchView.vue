<template>
  <main class="demo-launch-page">
    <section class="demo-launch-card">
      <span class="demo-badge">SIMULATED DEMO</span>
      <h1>Explore FleetOps live.</h1>
      <p>
        Open a short-lived, read-only workspace containing synthetic fleet data.
        No password, customer data, or external side effect is involved.
      </p>
      <p v-if="expired" class="alert alert-info">
        Your previous demo session expired safely. Launch a fresh session to
        continue.
      </p>
      <p v-if="error" class="alert alert-danger">{{ error }}</p>
      <button
        class="btn btn-primary btn-lg"
        type="button"
        :disabled="busy || !enabled"
        @click="launch"
      >
        {{ busy ? "Preparing demo..." : "Launch Live Demo" }}
      </button>
      <RouterLink class="demo-login-link" to="/login">
        Sign in to an existing workspace
      </RouterLink>
    </section>
  </main>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { useRoute, useRouter } from "vue-router";
import { useSessionStore } from "../features/auth/store";
import { apiRequest } from "../services/api";

const route = useRoute();
const router = useRouter();
const session = useSessionStore();
const enabled = ref(false);
const busy = ref(false);
const error = ref("");
const expired = computed(() => route.query.expired === "1");

onMounted(async () => {
  try {
    const status = await apiRequest<{ enabled: boolean }>(
      "/api/v1/demo/public/status",
    );
    enabled.value = status.enabled;
    if (!status.enabled)
      error.value = "The public demo is not enabled on this environment.";
  } catch {
    error.value = "The public demo status could not be loaded.";
  }
});

async function launch() {
  busy.value = true;
  error.value = "";
  try {
    await session.launchDemo();
    await router.push("/");
  } catch {
    error.value = session.error;
  } finally {
    busy.value = false;
  }
}
</script>
