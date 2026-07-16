import { createPinia, setActivePinia } from "pinia";
import { mount } from "@vue/test-utils";
import { nextTick } from "vue";
import AlertsView from "./AlertsView.vue";
import { useSessionStore } from "../features/auth/store";

describe("AlertsView", () => {
  beforeEach(() => {
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
  });

  it("renders alert actions, notifications, and admin setup panels", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(async (input: string | URL | Request) => {
        const url = String(input);
        if (url.includes("/api/v1/alerts/dashboard")) {
          return new Response(
            JSON.stringify({
              openCount: 3,
              acknowledgedCount: 1,
              criticalCount: 1,
              warningCount: 2,
              inactiveVehicleCount: 1,
              maintenanceCount: 1,
              complianceCount: 1,
              topAlerts: [],
              recentNotifications: [],
            }),
          );
        }

        if (url.includes("/api/v1/alerts/notifications")) {
          return new Response(
            JSON.stringify([
              {
                id: "notification-1",
                alertId: "alert-1",
                channel: 1,
                subject: "Expired insurance",
                body: "NW-100 document Insurance expires on 2026-07-15.",
                sentAtUtc: "2026-07-16T08:00:00Z",
              },
            ]),
          );
        }

        if (url.includes("/api/v1/alerts/assignees")) {
          return new Response(
            JSON.stringify([
              {
                userId: "user-2",
                fullName: "Northwind Operator",
                email: "operator@northwind.local",
                role: "Operator",
              },
            ]),
          );
        }

        if (url.includes("/api/v1/alerts") && !url.includes("/dashboard")) {
          return new Response(
            JSON.stringify([
              {
                id: "alert-1",
                ruleType: 5,
                severity: 2,
                status: 1,
                title: "Inactive vehicle",
                message: "NW-100 has no fresh telemetry. Last seen: never.",
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
            ]),
          );
        }

        if (url.includes("/api/v1/fleet/vehicles")) {
          return new Response(
            JSON.stringify([
              {
                id: "veh-1",
                registrationNumber: "NW-100",
                displayName: "Dispatch van",
                isActive: true,
                currentOdometerKm: 12000,
                rowVersion: 3,
              },
            ]),
          );
        }

        if (url.includes("/api/v1/fleet/drivers")) {
          return new Response(
            JSON.stringify([
              {
                id: "driver-1",
                fullName: "Alex North",
                licenseNumber: "NW-DL-001",
                phoneNumber: "+1-555-0100",
                isActive: true,
                rowVersion: 0,
              },
            ]),
          );
        }

        return new Response("not found", { status: 404 });
      }),
    );

    const wrapper = mount(AlertsView);
    await nextTick();
    await new Promise((resolve) => setTimeout(resolve, 0));
    await nextTick();

    expect(wrapper.text()).toContain("Alert center");
    expect(wrapper.text()).toContain("Inactive vehicle");
    expect(wrapper.text()).toContain("Assign");
    expect(wrapper.text()).toContain("Recent notifications");
    expect(wrapper.text()).toContain("Compliance setup");
    expect(wrapper.text()).toContain("Maintenance setup");
  });
});
