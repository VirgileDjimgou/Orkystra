import { createPinia, setActivePinia } from "pinia";
import { mount } from "@vue/test-utils";
import { nextTick } from "vue";
import { createRouter, createWebHistory } from "vue-router";
import DispatchView from "./DispatchView.vue";
import { useSessionStore } from "../features/auth/store";

describe("DispatchView", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    const session = useSessionStore();
    session.applySession({
      accessToken: "token",
      expiresAtUtc: "2099-01-01T00:00:00Z",
      user: {
        userId: "user-1",
        email: "operator@northwind.local",
        fullName: "Northwind Operator",
        organizationName: "Northwind Logistics",
        roles: ["Operator"],
      },
    });
  });

  it("renders missions, assignment context, and timeline", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(async (input: string | URL | Request) => {
        const url = String(input);
        if (url.includes("/api/v1/dispatch/missions/mission-1")) {
          return new Response(
            JSON.stringify({
              id: "mission-1",
              reference: "NW-M-100",
              title: "Morning downtown loop",
              status: 2,
              scheduledStartUtc: "2026-07-16T08:00:00Z",
              scheduledEndUtc: "2026-07-16T10:00:00Z",
              driverId: "driver-1",
              driverName: "Alex North",
              vehicleId: "veh-1",
              vehicleRegistrationNumber: "NW-100",
              simulatedDelayMinutes: 0,
              rowVersion: 3,
              latestInspection: {
                inspectionId: "inspection-1",
                outcome: 2,
                hasBlockingCriticalDefect: true,
                completedAtUtc: "2026-07-16T07:10:00Z",
                notes: "Front-right brake issue",
                items: [
                  {
                    sequence: 1,
                    code: "brakes",
                    label: "Brakes and steering",
                    isPass: false,
                    defectSeverity: 3,
                    notes: "Pedal pressure is unstable.",
                    photoReadUrl:
                      "/api/v1/media/asset-1?expires=123&signature=abc",
                  },
                ],
              },
              deliveryProofs: [
                {
                  proofId: "proof-1",
                  missionStopId: "stop-1",
                  recipientName: "Taylor Receiver",
                  signatureName: "Taylor Receiver",
                  deliveredAtUtc: "2026-07-16T09:30:00Z",
                  notes: "Left at reception",
                  photos: [
                    {
                      mediaAssetId: "asset-2",
                      caption: "Doorstep",
                      photoReadUrl:
                        "/api/v1/media/asset-2?expires=123&signature=abc",
                    },
                  ],
                },
              ],
              stops: [
                {
                  id: "stop-1",
                  sequence: 1,
                  name: "Depot",
                  address: "1 Dispatch Way",
                  plannedArrivalUtc: "2026-07-16T08:30:00Z",
                },
              ],
              timeline: [
                {
                  id: "event-1",
                  eventType: 0,
                  description: "Mission NW-M-100 created.",
                  occurredAtUtc: "2026-07-16T07:00:00Z",
                },
                {
                  id: "event-2",
                  eventType: 3,
                  description: "Mission status changed to Assigned.",
                  occurredAtUtc: "2026-07-16T07:20:00Z",
                },
              ],
            }),
          );
        }

        if (url.includes("/api/v1/dispatch/missions")) {
          return new Response(
            JSON.stringify([
              {
                id: "mission-1",
                reference: "NW-M-100",
                title: "Morning downtown loop",
                status: 2,
                scheduledStartUtc: "2026-07-16T08:00:00Z",
                scheduledEndUtc: "2026-07-16T10:00:00Z",
                driverId: "driver-1",
                driverName: "Alex North",
                vehicleId: "veh-1",
                vehicleRegistrationNumber: "NW-100",
                stopCount: 1,
                simulatedDelayMinutes: 0,
                rowVersion: 3,
                currentLatitude: 48.401,
                currentLongitude: 9.204,
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

        if (url.includes("/api/v1/fleet/vehicles")) {
          return new Response(
            JSON.stringify([
              {
                id: "veh-1",
                registrationNumber: "NW-100",
                displayName: "Dispatch van",
                isActive: true,
                currentOdometerKm: 0,
                rowVersion: 0,
              },
            ]),
          );
        }

        return new Response("not found", { status: 404 });
      }),
    );

    const router = createRouter({
      history: createWebHistory(),
      routes: [
        { path: "/", component: DispatchView },
        { path: "/map", component: { template: "<div />" } },
      ],
    });
    router.push("/");
    await router.isReady();

    const wrapper = mount(DispatchView, {
      global: {
        plugins: [router],
      },
    });

    await nextTick();
    await new Promise((resolve) => setTimeout(resolve, 0));
    await nextTick();

    expect(wrapper.text()).toContain("Mission control board");
    expect(wrapper.text()).toContain("NW-M-100");
    expect(wrapper.text()).toContain("Alex North");
    expect(wrapper.text()).toContain("Critical defect blocks departure");
    expect(wrapper.text()).toContain("Taylor Receiver");
    expect(wrapper.text()).toContain("StatusChanged");
    expect(wrapper.text()).toContain("Mission status changed to Assigned.");
    expect(wrapper.text()).toContain("Open in fleet map");
  });
});
