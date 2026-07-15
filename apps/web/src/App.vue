<template>
  <RouterView v-if="isLoginRoute" />
  <div v-else class="app-shell">
    <aside class="sidebar">
      <div class="brand-block">
        <div class="brand">Zynro Fleet</div>
        <div class="brand-subtitle">{{ session.user?.organizationName }}</div>
      </div>
      <nav class="nav flex-column gap-1">
        <RouterLink class="nav-link" to="/">Overview</RouterLink>
        <RouterLink class="nav-link" to="/map">Fleet map</RouterLink>
        <RouterLink class="nav-link" to="/fleet/vehicles">Vehicles</RouterLink>
        <RouterLink class="nav-link" to="/fleet/drivers">Drivers</RouterLink>
        <RouterLink class="nav-link" to="/fleet/devices">Devices</RouterLink>
        <RouterLink
          v-if="session.canManageUsers"
          class="nav-link"
          to="/admin/users"
        >
          User administration
        </RouterLink>
      </nav>
      <div class="sidebar-footer">
        <div class="identity-card">
          <strong>{{ session.user?.fullName }}</strong>
          <span>{{ session.user?.roles.join(", ") }}</span>
          <small>{{ session.user?.email }}</small>
        </div>
        <button class="btn btn-outline-light btn-sm w-100" @click="logout">
          Sign out
        </button>
      </div>
    </aside>
    <main class="main-content">
      <header class="topbar">
        <div>
          <strong>Operations control center</strong>
          <div class="text-secondary small">
            Role-aware fleet registry demonstration console
          </div>
        </div>
        <span class="badge text-bg-success">Authenticated session</span>
      </header>
      <section class="content"><RouterView /></section>
    </main>
  </div>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useRoute, useRouter } from "vue-router";
import { useSessionStore } from "./features/auth/store";

const route = useRoute();
const router = useRouter();
const session = useSessionStore();
const isLoginRoute = computed(() => route.path === "/login");

function logout() {
  session.logout();
  router.push("/login");
}
</script>
