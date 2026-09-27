<template>
  <nav class="cockpit-context-links" aria-label="Cockpit context">
    <RouterLink to="/overview">Overview</RouterLink>
    <RouterLink to="/alerts">Alerts</RouterLink>
    <RouterLink to="/map">Fleet map</RouterLink>
  </nav>
</template>

<script setup lang="ts">
import { RouterLink } from "vue-router";
</script>

<style scoped>
.cockpit-context-links {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
}

.cockpit-context-links a {
  padding: 0.35rem 0.6rem;
  border: 1px solid var(--border);
  border-radius: 999px;
  color: var(--text);
  font-size: 0.85rem;
  text-decoration: none;
}
</style>
