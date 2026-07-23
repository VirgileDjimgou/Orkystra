import { createRouter, createWebHistory } from "vue-router";
import { storeToRefs } from "pinia";
import { pinia } from "./pinia";
import { useSessionStore } from "./features/auth/store";
import DashboardView from "./views/DashboardView.vue";
import AlertsView from "./views/AlertsView.vue";
import AdminSecurityView from "./views/AdminSecurityView.vue";
import DispatchView from "./views/DispatchView.vue";
import DispatchProductivityView from "./views/DispatchProductivityView.vue";
import PilotReviewView from "./views/PilotReviewView.vue";
import DevicesView from "./views/DevicesView.vue";
import DriversView from "./views/DriversView.vue";
import FleetMapView from "./views/FleetMapView.vue";
import IntegrationsAdminView from "./views/IntegrationsAdminView.vue";
import LoginView from "./views/LoginView.vue";
import OperationsCenterView from "./views/OperationsCenterView.vue";
import OnboardingView from "./views/OnboardingView.vue";
import UsersAdminView from "./views/UsersAdminView.vue";
import VehiclesView from "./views/VehiclesView.vue";
import MaintenanceView from "./views/MaintenanceView.vue";
import ComplianceView from "./views/ComplianceView.vue";
import RecipientStatusView from "./views/RecipientStatusView.vue";

export const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: "/public/recipient-status/:token", component: RecipientStatusView, meta: { public: true } },
    { path: "/login", component: LoginView, meta: { guestOnly: true } },
    {
      path: "/admin/onboarding",
      component: OnboardingView,
      meta: {
        requiresAuth: true,
        requiresAdmin: true,
        title: "Guided setup",
        description: "Reach a first test mission without database access.",
      },
    },
    {
      path: "/admin/pilot",
      component: PilotReviewView,
      meta: {
        requiresAuth: true,
        requiresAdmin: true,
        title: "Pilot review",
        description:
          "Aggregate alpha evidence, support incidents, and niche decisions.",
      },
    },
    {
      path: "/",
      component: OperationsCenterView,
      meta: {
        requiresAuth: true,
        title: "Operations center",
        description:
          "One actionable queue for fleet exceptions and rapid triage.",
      },
    },
    {
      path: "/overview",
      component: DashboardView,
      meta: {
        requiresAuth: true,
        title: "Operations overview",
        description: "Priority signals and fleet readiness at a glance.",
      },
    },
    {
      path: "/alerts",
      component: AlertsView,
      meta: {
        requiresAuth: true,
        title: "Alert center",
        description: "Own, acknowledge and resolve fleet exceptions.",
      },
    },
    {
      path: "/maintenance",
      component: MaintenanceView,
      meta: {
        requiresAuth: true,
        title: "Maintenance work orders",
        description:
          "Keep vehicles available with scheduled, costed maintenance.",
      },
    },
    {
      path: "/compliance",
      component: ComplianceView,
      meta: {
        requiresAuth: true,
        title: "Compliance workspace",
        description:
          "Customer-configured document coverage and inspection campaigns.",
      },
    },
    {
      path: "/dispatch/missions",
      component: DispatchView,
      meta: {
        requiresAuth: true,
        title: "Mission board",
        description: "Plan, assign and follow work from dispatch to proof.",
      },
    },
    {
      path: "/dispatch/productivity",
      component: DispatchProductivityView,
      meta: {
        requiresAuth: true,
        title: "Dispatch productivity",
        description: "Reuse routes and safely import a daily mission plan.",
      },
    },
    {
      path: "/map",
      component: FleetMapView,
      meta: {
        requiresAuth: true,
        title: "Live fleet map",
        description: "Current positions, connection health and recent history.",
      },
    },
    {
      path: "/fleet/vehicles",
      component: VehiclesView,
      meta: {
        requiresAuth: true,
        title: "Vehicles",
        description: "Manage the active fleet and operational identity.",
      },
    },
    {
      path: "/fleet/drivers",
      component: DriversView,
      meta: {
        requiresAuth: true,
        title: "Drivers",
        description: "Manage driver availability and assignment readiness.",
      },
    },
    {
      path: "/fleet/devices",
      component: DevicesView,
      meta: {
        requiresAuth: true,
        title: "Tracking devices",
        description: "Connect devices and control active vehicle assignments.",
      },
    },
    {
      path: "/admin/users",
      component: UsersAdminView,
      meta: {
        requiresAuth: true,
        requiresAdmin: true,
        title: "User administration",
        description: "Provision role-aware access inside this organization.",
      },
    },
    {
      path: "/admin/security",
      component: AdminSecurityView,
      meta: {
        requiresAuth: true,
        requiresAdmin: true,
        title: "Security & data",
        description: "Control MFA, retention, export and tenant lifecycle.",
      },
    },
    {
      path: "/admin/integrations",
      component: IntegrationsAdminView,
      meta: {
        requiresAuth: true,
        requiresAdmin: true,
        title: "Integrations",
        description: "Manage scoped credentials, webhooks and data exchange.",
      },
    },
  ],
});

router.beforeEach(async (to) => {
  const session = useSessionStore(pinia);
  const { isAuthenticated, isAdmin } = storeToRefs(session);

  if (to.meta.public) {
    return true;
  }
  if (!session.initialized) {
    await session.hydrate();
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
