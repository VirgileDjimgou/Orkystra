import { createPinia, setActivePinia } from "pinia";
import { mount } from "@vue/test-utils";
import { nextTick } from "vue";
import { createRouter, createWebHistory } from "vue-router";
import FleetMapView from "./FleetMapView.vue";
import { useSessionStore } from "../features/auth/store";

vi.mock("../features/tracking/live", () => ({
  connectTrackingStream: vi.fn(async (_token, _onPosition, onStateChange) => {
    onStateChange("live");
    return {
      stop: vi.fn(async () => undefined),
    };
  }),
}));

vi.mock("leaflet", () => {
  const markerFactory = () => ({
    addTo() {
      return this;
    },
    on: vi.fn(),
    setLatLng: vi.fn(),
    setStyle: vi.fn(),
    bindPopup: vi.fn(),
    remove: vi.fn(),
  });
  const mapObject = {
    setView: vi.fn(() => mapObject),
    fitBounds: vi.fn(),
    panTo: vi.fn(),
    remove: vi.fn(),
  };

  return {
    default: {
      map: vi.fn(() => mapObject),
      tileLayer: vi.fn(() => ({ addTo: vi.fn() })),
      circleMarker: vi.fn(() => markerFactory()),
      latLngBounds: vi.fn((value) => value),
    },
  };
});

describe("FleetMapView", () => {
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

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("renders live positions, metrics, and history", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(async (input: string | URL | Request) => {
        const url = String(input);
        if (url.includes("/api/v1/tracking/positions")) {
          return new Response(
            JSON.stringify([
              {
                vehicleId: "veh-1",
                registrationNumber: "NW-100",
                displayName: "Dispatch van",
                deviceId: "NW-GPS-100",
                recordedAtUtc: "2026-07-15T10:00:00Z",
                latitude: 48.4,
                longitude: 9.2,
                speedKph: 42,
                headingDegrees: 180,
              },
              {
                vehicleId: "veh-2",
                registrationNumber: "NW-101",
                displayName: "Reserve van",
                deviceId: "NW-GPS-101",
                recordedAtUtc: "2026-07-15T10:00:05Z",
                latitude: 48.41,
                longitude: 9.21,
                speedKph: 30,
                headingDegrees: 90,
              },
            ]),
          );
        }

        if (url.includes("/api/v1/tracking/metrics")) {
          return new Response(
            JSON.stringify({
              currentVehicleCount: 2,
              historyPointCount: 6,
              acceptedCount: 6,
              duplicateCount: 1,
              outOfOrderCount: 1,
              retentionDays: 7,
            }),
          );
        }

        if (url.includes("/api/v1/tracking/history")) {
          return new Response(
            JSON.stringify({
              page: 1,
              pageSize: 5,
              totalCount: 2,
              items: [
                {
                  eventId: "evt-2",
                  vehicleId: "veh-1",
                  deviceId: "NW-GPS-100",
                  recordedAtUtc: "2026-07-15T10:00:00Z",
                  ingestedAtUtc: "2026-07-15T10:00:01Z",
                  latitude: 48.4,
                  longitude: 9.2,
                  speedKph: 42,
                  headingDegrees: 180,
                },
              ],
            }),
          );
        }

        return new Response("not found", { status: 404 });
      }),
    );

    const router = createRouter({
      history: createWebHistory(),
      routes: [{ path: "/", component: FleetMapView }],
    });
    router.push("/");
    await router.isReady();

    const wrapper = mount(FleetMapView, {
      global: {
        plugins: [router],
      },
    });
    await nextTick();
    await new Promise((resolve) => setTimeout(resolve, 0));
    await nextTick();

    expect(wrapper.text()).toContain("Fleet map");
    expect(wrapper.text()).toContain("NW-100");
    expect(wrapper.text()).toContain("NW-101");
    expect(wrapper.text()).toContain("Duplicates ignored");
    expect(wrapper.text()).toContain("evt-2");
    expect(wrapper.text()).toContain("Live stream");
  });
});
