import { createPinia, setActivePinia } from "pinia";
import { mount } from "@vue/test-utils";
import { createRouter, createWebHistory } from "vue-router";
import { nextTick } from "vue";
import OperationsCockpitView from "./OperationsCockpitView.vue";
import { useSessionStore } from "../features/auth/store";

vi.mock("../features/tracking/live", () => ({
  connectTrackingStream: vi.fn(async (_token, _position, onState) => {
    onState("live");
    return { stop: vi.fn(async () => undefined) };
  }),
}));
vi.mock("../features/operations/live", () => ({
  connectOperationsStream: vi.fn(async () => ({
    stop: vi.fn(async () => undefined),
  })),
}));
vi.mock("leaflet", () => ({
  default: {
    map: vi.fn(() => ({ setView: vi.fn(), panTo: vi.fn(), remove: vi.fn() })),
    tileLayer: vi.fn(() => ({ addTo: vi.fn() })),
    circleMarker: vi.fn(() => ({
      addTo() {
        return this;
      },
      on: vi.fn(),
      setLatLng: vi.fn(),
      setStyle: vi.fn(),
      bindPopup: vi.fn(),
      remove: vi.fn(),
    })),
    polyline: vi.fn(() => {
      const layer = { addTo: vi.fn(), remove: vi.fn() };
      layer.addTo.mockReturnValue(layer);
      return layer;
    }),
  },
}));

describe("OperationsCockpitView", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    useSessionStore().applySession({
      accessToken: "token",
      expiresAtUtc: "2099-01-01T00:00:00Z",
      user: {
        userId: "user",
        email: "operator@northwind.local",
        fullName: "Northwind Operator",
        organizationName: "Northwind Logistics",
        roles: ["Operator"],
      },
    });
    vi.stubGlobal(
      "fetch",
      vi.fn(async (input: string | URL | Request) => {
        const url = String(input);
        if (url.includes("tracking/positions"))
          return json([
            {
              vehicleId: "vehicle-1",
              registrationNumber: "NW-100",
              displayName: "Dispatch van",
              deviceId: "gps-1",
              recordedAtUtc: "2026-09-26T10:00:00Z",
              latitude: 48.4,
              longitude: 9.2,
              speedKph: 42,
              headingDegrees: 90,
              qualityStatus: "Fresh",
            },
          ]);
        if (url.includes("tracking/metrics"))
          return json({
            currentVehicleCount: 1,
            historyPointCount: 1,
            acceptedCount: 1,
            duplicateCount: 0,
            outOfOrderCount: 0,
            retentionDays: 7,
          });
        if (url.includes("tracking/history"))
          return json({
            page: 1,
            pageSize: 40,
            totalCount: 2,
            items: [
              {
                eventId: "history-1",
                vehicleId: "vehicle-1",
                deviceId: "gps-1",
                recordedAtUtc: "2026-09-26T09:59:00Z",
                ingestedAtUtc: "2026-09-26T09:59:00Z",
                latitude: 48.39,
                longitude: 9.19,
                speedKph: 40,
                headingDegrees: 90,
              },
              {
                eventId: "history-2",
                vehicleId: "vehicle-1",
                deviceId: "gps-1",
                recordedAtUtc: "2026-09-26T10:00:00Z",
                ingestedAtUtc: "2026-09-26T10:00:00Z",
                latitude: 48.4,
                longitude: 9.2,
                speedKph: 42,
                headingDegrees: 90,
              },
            ],
          });
        if (url.includes("tracking/diagnostics"))
          return json([
            {
              vehicleId: "vehicle-1",
              registrationNumber: "NW-100",
              displayName: "Dispatch van",
              driverName: "Alex North",
              deviceId: "gps-1",
              lastCommunicationAtUtc: "2026-09-26T10:00:00Z",
              status: "Fresh",
              reason: "Position is reliable.",
              qualityScore: 100,
              accuracyMeters: 8,
              source: "simulator",
              sequenceNumber: 1,
            },
          ]);
        if (url.includes("operations/exceptions"))
          return json({
            summary: {
              totalActive: 1,
              criticalCount: 0,
              warningCount: 1,
              snoozedCount: 0,
              unassignedCount: 1,
            },
            items: [
              {
                id: "exception-1",
                sourceType: "Alert",
                severity: "Warning",
                workflowStatus: "Open",
                title: "Tyre pressure",
                message: "Review required",
                detectedAtUtc: "2026-09-26T10:00:00Z",
                snoozedUntilUtc: null,
                snoozeReason: null,
                resolvedAtUtc: null,
                resolutionReason: null,
                assignedToUserId: null,
                assignedToDisplayName: null,
                acknowledgedByUserId: null,
                acknowledgedByDisplayName: null,
                searchText: "",
                sourceRowVersion: 1,
                stateRowVersion: 1,
                concurrencyToken: "token",
                links: {
                  missionId: "mission-1",
                  missionReference: "NW-1",
                  vehicleId: "vehicle-1",
                  vehicleRegistrationNumber: "NW-100",
                  driverId: null,
                  driverName: null,
                  alertId: "alert-1",
                  inspectionId: null,
                  syncIncidentId: null,
                },
              },
            ],
          });
        if (url.includes("demo/agent-activities"))
          return json([
            {
              id: "activity-1",
              agentId: "agent-1",
              driverId: "driver-1",
              vehicleId: "vehicle-1",
              missionId: "mission-1",
              sequence: 3,
              observedState: "EnRoute",
              policy: "schedule-delay",
              action: "ReportDelay",
              resultCode: "delay-reported",
              resultMessage: "Controlled 15-minute delay recorded.",
              occurredAtUtc: "2026-09-26T10:00:00Z",
            },
          ]);
        if (url.includes("dispatch/missions"))
          return json([
            {
              id: "mission-1",
              reference: "NW-1",
              title: "Morning delivery",
              status: "Assigned",
              scheduledStartUtc: "2026-09-26T10:00:00Z",
              scheduledEndUtc: "2026-09-26T12:00:00Z",
              driverId: "driver-1",
              driverName: "Alex North",
              vehicleId: "vehicle-1",
              vehicleRegistrationNumber: "NW-100",
              stopCount: 2,
              simulatedDelayMinutes: 0,
              rowVersion: 1,
              currentLatitude: 48.4,
              currentLongitude: 9.2,
            },
          ]);
        return new Response("not found", { status: 404 });
      }),
    );
  });

  it("presents a map-first cockpit and resolves exception context", async () => {
    const router = createRouter({
      history: createWebHistory(),
      routes: [{ path: "/", component: OperationsCockpitView }],
    });
    await router.push("/?vehicleId=vehicle-1");
    await router.isReady();
    const wrapper = mount(OperationsCockpitView, {
      global: { plugins: [router] },
    });
    await new Promise((resolve) => setTimeout(resolve, 0));

    expect(wrapper.get("h1").text()).toBe("Operations cockpit");
    expect(wrapper.text()).toContain("NW-100");
    await wrapper.get(".cockpit-list-item").trigger("click");
    expect(wrapper.text()).toContain("Tyre pressure");

    await wrapper.get('input[type="range"]').setValue("360");
    expect(wrapper.get(".cockpit-dock").attributes("style")).toContain(
      "--cockpit-dock-height: 360px",
    );
    await wrapper.get('button[aria-expanded="true"]').trigger("click");
    expect(wrapper.get('button[aria-expanded="false"]').text()).toBe(
      "Expand activity",
    );
    await wrapper.get('button[aria-expanded="false"]').trigger("click");

    await wrapper
      .findAll('button[role="tab"]')
      .find((tab) => tab.text() === "Missions")!
      .trigger("click");
    await nextTick();
    await wrapper.get(".cockpit-list-item").trigger("click");
    expect(wrapper.text()).toContain("Morning delivery");

    await wrapper
      .findAll('button[role="tab"]')
      .find((tab) => tab.text() === "Virtual drivers")!
      .trigger("click");
    await nextTick();
    expect(wrapper.text()).toContain("ReportDelay · EnRoute");
    expect(wrapper.text()).toContain("schedule-delay · delay-reported");
    expect(wrapper.text()).not.toContain("chain-of-thought");
  });
});

function json(value: unknown) {
  return new Response(JSON.stringify(value), {
    headers: { "Content-Type": "application/json" },
  });
}
