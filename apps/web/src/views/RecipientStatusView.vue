<template>
  <main class="container py-5" aria-live="polite">
    <h1>Delivery status</h1>
    <p v-if="loading">Loading your delivery status…</p>
    <div v-else-if="status" class="card p-4">
      <p><strong>Status:</strong> {{ status.status }}</p>
      <p><strong>Estimated arrival:</strong> {{ status.etaWindow }}</p>
      <p class="text-muted">
        This estimate is based on the latest available information.
      </p>
      <form class="border-top pt-3 mt-3" @submit.prevent="correctContact">
        <!-- prettier-ignore -->
        <label class="form-label" for="recipient-email">Notification email</label>
        <p class="small text-muted">
          Only use this if your delivery notifications should go to a different
          address.
        </p>
        <div class="input-group">
          <input
            id="recipient-email"
            v-model="email"
            class="form-control"
            type="email"
            autocomplete="email"
            required
          />
          <button
            class="btn btn-outline-primary"
            :disabled="saving"
            type="submit"
          >
            Save
          </button>
        </div>
        <p v-if="contactMessage" class="small mt-2 mb-0" role="status">
          {{ contactMessage }}
        </p>
      </form>
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
const email = ref("");
const saving = ref(false);
const contactMessage = ref("");

onMounted(async () => {
  try {
    status.value = await apiRequest<RecipientStatus>(
      `/public/v1/recipient-status/${String(route.params.token)}`,
    );
  } catch {
    status.value = null;
  } finally {
    loading.value = false;
  }
});

async function correctContact() {
  saving.value = true;
  contactMessage.value = "";
  try {
    await apiRequest<void>(
      `/public/v1/recipient-status/${String(route.params.token)}/contact`,
      {
        method: "POST",
        body: { email: email.value },
      },
    );
    email.value = "";
    contactMessage.value = "Your notification email has been updated.";
  } catch {
    contactMessage.value =
      "We could not update this email. Please check the address and try again.";
  } finally {
    saving.value = false;
  }
}
</script>
