<template>
  <div>
    <div class="d-flex justify-content-between align-items-center mb-4">
      <div>
        <h1 class="h3 mb-1">Vue générale</h1>
        <p class="text-secondary mb-0">Base UI du sprint de fondation.</p>
      </div>
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
  </div>
</template>

<script setup lang="ts">
const cards = [
  {
    label: "Véhicules actifs",
    value: "0",
    note: "Connecter le simulateur GPS",
  },
  { label: "Missions en cours", value: "0", note: "Disponible au sprint 04" },
  { label: "Alertes ouvertes", value: "0", note: "Disponible au sprint 07" },
  { label: "Synchronisations", value: "0", note: "Disponible au sprint 05" },
];
</script>
