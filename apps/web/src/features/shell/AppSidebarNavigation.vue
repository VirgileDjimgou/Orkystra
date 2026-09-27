<template>
  <nav aria-label="Primary navigation">
    <section
      v-for="group in visibleGroups"
      :key="group.label"
      class="nav-group"
    >
      <p class="nav-group-label">{{ group.label }}</p>
      <RouterLink
        v-for="item in group.items"
        :key="item.to"
        class="nav-link"
        :to="item.to"
      >
        <span class="nav-icon" aria-hidden="true">{{ item.icon }}</span>
        <span>{{ item.label }}</span>
      </RouterLink>
    </section>
  </nav>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { primaryNavigation } from "./navigation";

const props = defineProps<{ isAdmin: boolean }>();

const visibleGroups = computed(() =>
  primaryNavigation
    .map((group) => ({
      ...group,
      items: group.items.filter((item) => !item.adminOnly || props.isAdmin),
    }))
    .filter((group) => group.items.length > 0),
);
</script>
