import { createPinia, setActivePinia } from "pinia";
import { mount } from "@vue/test-utils";
import { nextTick } from "vue";
import OperationsCenterView from "./OperationsCenterView.vue";
import { useSessionStore } from "../features/auth/store";

vi.mock("../features/operations/live", () => ({
  connectOperationsStream: vi.fn(async (_onQueueChanged, onStateChange) => {
    onStateChange("live");
    return { stop: vi.fn(async () => undefined) };
  }),
}));

describe("OperationsCenterView", () => {
  it("renders the unified exception queue and saved views", async () => {
    setActivePinia(createPinia());
    const session = useSessionStore();
    session.applySession({
      accessToken: "token",
      expiresAtUtc: "2099-01-01T00:00:00Z",
      csrfToken: "csrf",
      user: {
        userId: "user-1",
        email: "operator@northwind.local",
        fullName: "Northwind Operator",
        organizationName: "Northwind Logistics",
        roles: ["Operator"],
      },
    });

    vi.stubGlobal(
      "fetch",
      vi.fn(async (input: RequestInfo | URL) => {
        const url = String(input);
        if (url.includes("/api/v1/operations/exceptions")) {
          return new Response(
            JSON.stringify({
              summary: {
                totalActive: 3,
                criticalCount: 2,
                warningCount: 1,
                snoozedCount: 0,
                unassignedCount: 1,
              },
              items: [
                {
                  id: "delay:mission-1",
                  sourceType: "MissionDelay",
                  severity: "Critical",
                  workflowStatus: "Open",
                  title: "Mission NW-OPS-1 delayed",
                  message: "The mission is delayed by 45 minute(s).",
                  detectedAtUtc: "2026-07-17T08:00:00Z",
                  snoozedUntilUtc: null,
                  snoozeReason: null,
                  resolvedAtUtc: null,
                  resolutionReason: null,
                  assignedToUserId: null,
                  assignedToDisplayName: null,
                  acknowledgedByUserId: null,
                  acknowledgedByDisplayName: null,
                  searchText: "NW-OPS-1 delayed",
                  sourceRowVersion: 3,
                  stateRowVersion: 0,
                  concurrencyToken: "3:0:1",
                  links: {
                    missionId: "mission-1",
                    missionReference: "NW-OPS-1",
                    vehicleId: "vehicle-1",
                    vehicleRegistrationNumber: "NW-100",
                    driverId: "driver-1",
                    driverName: "Taylor Driver",
                    alertId: null,
                    inspectionId: null,
                    syncIncidentId: null,
                  },
                },
              ],
            }),
          );
        }

        if (url.includes("/api/v1/operations/saved-views")) {
          return new Response(
            JSON.stringify([
              {
                id: "view-1",
                name: "Morning triage",
                isShared: true,
                filters: {
                  search: "",
                  sourceType: "MissionDelay",
                  severity: "Critical",
                  workflowStatus: "Open",
                  assignedToUserId: null,
                  includeSnoozed: false,
                },
                rowVersion: 0,
                createdByUserId: "user-1",
              },
            ]),
          );
        }

        if (url.includes("/api/v1/alerts/assignees")) {
          return new Response(
            JSON.stringify([
              {
                userId: "user-1",
                fullName: "Northwind Operator",
                email: "operator@northwind.local",
                role: "Operator",
              },
            ]),
          );
        }

        return new Response("Not found", { status: 404 });
      }),
    );

    const wrapper = mount(OperationsCenterView, {
      global: {
        stubs: {
          RouterLink: {
            template: "<a><slot /></a>",
          },
        },
      },
    });

    await nextTick();
    await new Promise((resolve) => setTimeout(resolve, 0));
    await nextTick();

    expect(wrapper.text()).toContain("Operations center");
    expect(wrapper.text()).toContain("Unified exception queue");
    expect(wrapper.text()).toContain("Mission NW-OPS-1 delayed");
    expect(wrapper.text()).toContain("Morning triage");
    expect(wrapper.text()).toContain("Live");
  });
});
