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
      <span class="badge text-bg-dark">{{
        session.user?.roles.join(", ")
      }}</span>
    </section>

    <div class="row g-3">
      <div v-for="card in cards" :key="card.label" class="col-sm-6 col-xl-3">
        <div class="metric-card">
          <span class="text-secondary">{{ card.label }}</span>
          <strong class="display-6">{{ card.value }}</strong>
          <small>{{ card.note }}</small>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useSessionStore } from "../features/auth/store";

const session = useSessionStore();

const cards = computed(() => [
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
    label: "Fleet telemetry",
    value: "Scoped",
    note: "Latest positions filtered per tenant",
  },
]);
</script>
