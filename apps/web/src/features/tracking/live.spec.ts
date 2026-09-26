import { completeTrackingReconnect, createPositionBatcher } from "./live";
import type { TrackingPositionResponse } from "./contracts";

const position = (
  vehicleId: string,
  recordedAtUtc: string,
): TrackingPositionResponse => ({
  vehicleId,
  registrationNumber: vehicleId,
  displayName: vehicleId,
  deviceId: `${vehicleId}-device`,
  recordedAtUtc,
  latitude: 48.4,
  longitude: 9.2,
  speedKph: 30,
  headingDegrees: 90,
});

describe("createPositionBatcher", () => {
  afterEach(() => vi.useRealTimers());

  it("keeps the latest position per vehicle without dropping other vehicles", () => {
    vi.useFakeTimers();
    const received: TrackingPositionResponse[] = [];
    const batcher = createPositionBatcher((item) => received.push(item));

    batcher.enqueue(position("vehicle-a", "2026-09-26T10:00:00Z"));
    batcher.enqueue(position("vehicle-b", "2026-09-26T10:00:01Z"));
    batcher.enqueue(position("vehicle-a", "2026-09-26T10:00:02Z"));
    vi.advanceTimersByTime(250);

    expect(received).toHaveLength(2);
    expect(received).toEqual([
      position("vehicle-a", "2026-09-26T10:00:02Z"),
      position("vehicle-b", "2026-09-26T10:00:01Z"),
    ]);
  });

  it("waits for a tenant snapshot catch-up before marking a reconnect live", async () => {
    const events: string[] = [];

    await completeTrackingReconnect(
      (state) => events.push(state),
      async () => {
        events.push("catch-up");
      },
    );

    expect(events).toEqual(["catch-up", "live"]);
  });
});
