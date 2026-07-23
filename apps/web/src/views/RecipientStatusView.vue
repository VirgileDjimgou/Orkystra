<template>
  <main class="container py-5" aria-live="polite">
    <h1>Delivery status</h1>
    <p v-if="loading">Loading your delivery status…</p>
    <div v-else-if="status" class="card p-4">
      <p><strong>Status:</strong> {{ status.status }}</p>
      <p><strong>Estimated arrival:</strong> {{ status.etaWindow }}</p>
      <p class="text-muted">This estimate is based on the latest available information.</p>
    </div>
    <p v-else role="alert">This delivery link is unavailable or has expired.</p>
  </main>
</template>

<script setup lang="ts">
import { onMounted, ref } from "vue";
import { useRoute } from "vue-router";
import { apiRequest } from "../services/api";

type RecipientStatus = { status: string; etaWindow: string };
const route = useRoute();
const loading = ref(true);
const status = ref<RecipientStatus | null>(null);

onMounted(async () => {
  try {
    status.value = await apiRequest<RecipientStatus>(`/public/v1/recipient-status/${String(route.params.token)}`);
  } catch {
    status.value = null;
  } finally {
    loading.value = false;
  }
});
</script>
