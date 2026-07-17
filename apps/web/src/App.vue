<template>
  <RouterView v-if="isLoginRoute" />
  <template v-else>
    <a class="skip-link" href="#main-content">Skip to main content</a>
    <div class="app-shell">
      <aside class="sidebar" :class="{ 'sidebar-open': isNavigationOpen }">
        <div class="brand-block">
          <div class="brand-mark" aria-hidden="true">O</div>
          <div>
            <div class="brand">Orkystra</div>
            <div class="brand-subtitle">
              {{ session.user?.organizationName }}
            </div>
          </div>
          <button
            class="sidebar-close"
            type="button"
            aria-label="Close navigation"
            @click="isNavigationOpen = false"
          >
            ×
          </button>
        </div>

        <nav aria-label="Primary navigation">
          <section
            v-for="group in visibleNavigation"
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

        <div class="sidebar-footer">
          <div class="workspace-state">
            <span class="workspace-dot" aria-hidden="true"></span>
            <span>Operational workspace</span>
          </div>
          <div class="identity-card">
            <span class="identity-avatar" aria-hidden="true">{{
              userInitials
            }}</span>
            <span class="identity-copy">
              <strong>{{ session.user?.fullName }}</strong>
              <small>{{ session.user?.roles.join(" · ") }}</small>
            </span>
          </div>
          <button class="btn btn-sidebar w-100" type="button" @click="logout">
            Sign out
          </button>
        </div>
      </aside>

      <button
        v-if="isNavigationOpen"
        class="navigation-scrim"
        type="button"
        aria-label="Close navigation"
        @click="isNavigationOpen = false"
      ></button>

      <main id="main-content" class="main-content" tabindex="-1">
        <header class="topbar">
          <div class="topbar-context">
            <button
              class="navigation-toggle"
              type="button"
              aria-label="Open navigation"
              :aria-expanded="isNavigationOpen"
              @click="isNavigationOpen = true"
            >
              <span></span><span></span><span></span>
            </button>
            <div>
              <span class="topbar-eyebrow">Operations workspace</span>
              <strong>{{ pageTitle }}</strong>
              <small>{{ pageDescription }}</small>
            </div>
          </div>
          <div class="topbar-actions">
            <RouterLink class="quick-link" to="/map">Live map</RouterLink>
            <RouterLink class="btn btn-primary" to="/dispatch/missions">
              Mission board
            </RouterLink>
          </div>
        </header>
        <section class="content"><RouterView /></section>
      </main>
    </div>
  </template>
</template>

<script setup lang="ts">
import { computed, ref, watch } from "vue";
import { useRoute, useRouter } from "vue-router";
import { useSessionStore } from "./features/auth/store";

type NavigationItem = {
  label: string;
  to: string;
  icon: string;
  adminOnly?: boolean;
};

const route = useRoute();
const router = useRouter();
const session = useSessionStore();
const isNavigationOpen = ref(false);
const isLoginRoute = computed(() => route.path === "/login");
const pageTitle = computed(() =>
  String(route.meta.title ?? "Operations overview"),
);
const pageDescription = computed(() =>
  String(
    route.meta.description ?? "Monitor the work that needs attention now.",
  ),
);
const userInitials = computed(() =>
  (session.user?.fullName ?? "Fleet user")
    .split(/\s+/)
    .slice(0, 2)
    .map((part) => part.charAt(0).toUpperCase())
    .join(""),
);

const navigation: Array<{ label: string; items: NavigationItem[] }> = [
  {
    label: "Operate",
    items: [
      { label: "Operations center", to: "/", icon: "01" },
      { label: "Overview", to: "/overview", icon: "02" },
      { label: "Alert center", to: "/alerts", icon: "03" },
      { label: "Maintenance", to: "/maintenance", icon: "M" },
      { label: "Compliance", to: "/compliance", icon: "C" },
      { label: "Dispatch", to: "/dispatch/missions", icon: "04" },
      { label: "Live map", to: "/map", icon: "05" },
    ],
  },
  {
    label: "Fleet",
    items: [
      { label: "Vehicles", to: "/fleet/vehicles", icon: "V" },
      { label: "Drivers", to: "/fleet/drivers", icon: "D" },
      { label: "Devices", to: "/fleet/devices", icon: "G" },
    ],
  },
  {
    label: "Administration",
    items: [
      {
        label: "Guided setup",
        to: "/admin/onboarding",
        icon: "✓",
        adminOnly: true,
      },
      { label: "Users", to: "/admin/users", icon: "U", adminOnly: true },
      {
        label: "Security & data",
        to: "/admin/security",
        icon: "S",
        adminOnly: true,
      },
      {
        label: "Integrations",
        to: "/admin/integrations",
        icon: "I",
        adminOnly: true,
      },
    ],
  },
];

const visibleNavigation = computed(() =>
  navigation
    .map((group) => ({
      ...group,
      items: group.items.filter((item) => !item.adminOnly || session.isAdmin),
    }))
    .filter((group) => group.items.length > 0),
);

watch(
  () => route.fullPath,
  () => {
    isNavigationOpen.value = false;
  },
);

function logout() {
  session.logout();
  router.push("/login");
}
</script>
