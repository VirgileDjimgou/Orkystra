import { createPinia, setActivePinia } from "pinia";
import { mount } from "@vue/test-utils";
import { nextTick } from "vue";
import DashboardView from "./DashboardView.vue";
import { useSessionStore } from "../features/auth/store";

describe("DashboardView", () => {
  it("renders alert-aware operational metrics", async () => {
    setActivePinia(createPinia());
    const session = useSessionStore();
    session.applySession({
      accessToken: "token",
      expiresAtUtc: "2099-01-01T00:00:00Z",
      user: {
        userId: "user-1",
        email: "admin@northwind.local",
        fullName: "Northwind Admin",
        organizationName: "Northwind Logistics",
        roles: ["Admin"],
      },
    });

    vi.stubGlobal(
      "fetch",
      vi.fn(
        async () =>
          new Response(
            JSON.stringify({
              openCount: 4,
              acknowledgedCount: 1,
              criticalCount: 2,
              warningCount: 2,
              inactiveVehicleCount: 1,
              maintenanceCount: 1,
              complianceCount: 2,
              topAlerts: [
                {
                  id: "alert-1",
                  ruleType: 1,
                  severity: 3,
                  status: 1,
                  title: "Expired insurance",
                  message: "NW-100 document Insurance expires on 2026-07-15.",
                  targetType: "vehicle",
                  targetEntityId: "veh-1",
                  targetLabel: "NW-100",
                  assignedToUserId: null,
                  assignedToDisplayName: null,
                  acknowledgedByUserId: null,
                  acknowledgedByDisplayName: null,
                  lastDetectedAtUtc: "2026-07-16T08:00:00Z",
                  assignedAtUtc: null,
                  acknowledgedAtUtc: null,
                  resolvedAtUtc: null,
                  rowVersion: 0,
                },
              ],
              recentNotifications: [
                {
                  id: "notification-1",
                  alertId: "alert-1",
                  channel: 1,
                  subject: "Expired insurance",
                  body: "NW-100 document Insurance expires on 2026-07-15.",
                  sentAtUtc: "2026-07-16T08:00:00Z",
                },
              ],
            }),
          ),
      ),
    );

    const wrapper = mount(DashboardView);
    await nextTick();
    await new Promise((resolve) => setTimeout(resolve, 0));
    await nextTick();

    expect(wrapper.text()).toContain("Operations overview");
    expect(wrapper.text()).toContain("4");
    expect(wrapper.text()).toContain("Critical");
    expect(wrapper.text()).toContain("Expired insurance");
    expect(wrapper.text()).toContain("Recent notifications");
  });
});
