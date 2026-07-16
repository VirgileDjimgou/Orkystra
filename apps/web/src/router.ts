import { createRouter, createWebHistory } from "vue-router";
import { storeToRefs } from "pinia";
import { pinia } from "./pinia";
import { useSessionStore } from "./features/auth/store";
import DashboardView from "./views/DashboardView.vue";
import AlertsView from "./views/AlertsView.vue";
import AdminSecurityView from "./views/AdminSecurityView.vue";
import DispatchView from "./views/DispatchView.vue";
import DevicesView from "./views/DevicesView.vue";
import DriversView from "./views/DriversView.vue";
import FleetMapView from "./views/FleetMapView.vue";
import IntegrationsAdminView from "./views/IntegrationsAdminView.vue";
import LoginView from "./views/LoginView.vue";
import UsersAdminView from "./views/UsersAdminView.vue";
import VehiclesView from "./views/VehiclesView.vue";

export const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: "/login", component: LoginView, meta: { guestOnly: true } },
    { path: "/", component: DashboardView, meta: { requiresAuth: true } },
    {
      path: "/alerts",
      component: AlertsView,
      meta: { requiresAuth: true },
    },
    {
      path: "/dispatch/missions",
      component: DispatchView,
      meta: { requiresAuth: true },
    },
    { path: "/map", component: FleetMapView, meta: { requiresAuth: true } },
    {
      path: "/fleet/vehicles",
      component: VehiclesView,
      meta: { requiresAuth: true },
    },
    {
      path: "/fleet/drivers",
      component: DriversView,
      meta: { requiresAuth: true },
    },
    {
      path: "/fleet/devices",
      component: DevicesView,
      meta: { requiresAuth: true },
    },
    {
      path: "/admin/users",
      component: UsersAdminView,
      meta: { requiresAuth: true, requiresAdmin: true },
    },
    {
      path: "/admin/security",
      component: AdminSecurityView,
      meta: { requiresAuth: true, requiresAdmin: true },
    },
    {
      path: "/admin/integrations",
      component: IntegrationsAdminView,
      meta: { requiresAuth: true, requiresAdmin: true },
    },
  ],
});

router.beforeEach(async (to) => {
  const session = useSessionStore(pinia);
  const { isAuthenticated, isAdmin } = storeToRefs(session);

  if (!session.user && session.status === "anonymous") {
    session.hydrate();
  }
  if (session.isAuthenticated && !session.user) {
    await session.refreshProfile();
  }

  if (to.meta.requiresAuth && !isAuthenticated.value) {
    return { path: "/login" };
  }
  if (to.meta.guestOnly && isAuthenticated.value) {
    return { path: "/" };
  }
  if (to.meta.requiresAdmin && !isAdmin.value) {
    return { path: "/" };
  }

  return true;
});
