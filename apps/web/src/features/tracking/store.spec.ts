import { createPinia, setActivePinia } from "pinia";
import type { TrackingPositionResponse } from "./contracts";
import { useTrackingStore } from "./store";

const position = (
  overrides: Partial<TrackingPositionResponse> = {},
): TrackingPositionResponse => ({
  vehicleId: "vehicle-a",
  registrationNumber: "NW-100",
  displayName: "Dispatch van",
  deviceId: "NW-GPS-100",
  recordedAtUtc: "2026-09-26T10:00:00Z",
  latitude: 48.4,
  longitude: 9.2,
  speedKph: 30,
  headingDegrees: 90,
  sequenceNumber: 10,
  accuracyMeters: 8,
  source: "device",
  qualityScore: 100,
  qualityStatus: "Fresh",
  qualityReason: "Position is reliable.",
  ...overrides,
});

describe("tracking store live updates", () => {
  beforeEach(() => setActivePinia(createPinia()));

  it("updates all mutable metadata when a live quality transition arrives", () => {
    const store = useTrackingStore();
    store.positions = [position()];

    store.applyLivePosition(
      position({
        recordedAtUtc: "2026-09-26T10:00:01Z",
        sequenceNumber: 11,
        accuracyMeters: 150,
        source: "simulator",
        qualityScore: 65,
        qualityStatus: "Inaccurate",
        qualityReason: "GPS accuracy is above 100 metres.",
      }),
    );

    expect(store.positions[0]).toMatchObject({
      sequenceNumber: 11,
      accuracyMeters: 150,
      source: "simulator",
      qualityScore: 65,
      qualityStatus: "Inaccurate",
      qualityReason: "GPS accuracy is above 100 metres.",
    });
  });

  it("rejects stale live updates", () => {
    const store = useTrackingStore();
    store.positions = [
      position({ recordedAtUtc: "2026-09-26T10:00:10Z", latitude: 48.5 }),
    ];

    store.applyLivePosition(
      position({ recordedAtUtc: "2026-09-26T10:00:09Z", latitude: 48.1 }),
    );

    expect(store.positions[0].latitude).toBe(48.5);
  });
});
